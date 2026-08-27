//#define KEEP_WINDOW_OPEN
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.InteropServices;
using Serilog;
using Solace.Common.Utils;

namespace Solace.Common;

// from https://stackoverflow.com/a/50311340/15878562
public sealed class ConsoleProcess : IDisposable
{
    private readonly string _filePath;
    public readonly Process Process = new Process();

    public bool IORedirected { get; private set; }
    public bool OpenInNewWindow { get; private set; }

    public event DataReceivedEventHandler? ErrorTextReceived
    {
        add => Process.ErrorDataReceived += value;
        remove => Process.ErrorDataReceived -= value;
    }
    public event EventHandler? ProcessExited;
    public event DataReceivedEventHandler? StandartTextReceived
    {
        add => Process.OutputDataReceived += value;
        remove => Process.OutputDataReceived -= value;
    }

    public int? ExitCode
    {
        get
        {
            try
            {
                return ActualProcess.ExitCode;
            }
            catch (InvalidOperationException)
            {
                return null;
            }
        }
    }

    public string ExitCodeText => ExitCode is { } exitCode ? exitCode.ToString(CultureInfo.InvariantCulture) : "Unknown"; // TODO 

    public int Id => _actualAppPid is null ? Process.Id : _actualAppPid.Value;

    private bool running;

    private string? _pidFilePath;
    private int? _actualAppPid;
    private Process? _cachedActualProcess;

    private static string? _cachedLinuxTerminal;
    private static string? _cachedLinuxTerminalExecArg;
    private static bool _linuxTerminalDiscoveryAttempted;
    private static readonly Lock _terminalCacheLock = new Lock();

    private static readonly (string Name, string ExecutionArg)[] _linuxTerminalsToCheck =
    [
        ("x-terminal-emulator", "-e"),
        ("gnome-terminal", "--"),
        ("konsole", "-e"),
        ("xfce4-terminal", "-e"),
        ("alacritty", "-e"),
        ("kitty", "--"),
        ("xterm", "-e"),
    ];

    private Process ActualProcess
    {
        get
        {
            if (_actualAppPid is null)
            {
                return Process;
            }

            if (_cachedActualProcess is not null)
            {
                return _cachedActualProcess;
            }

            try
            {
                _cachedActualProcess = Process.GetProcessById(_actualAppPid.Value);
                return _cachedActualProcess;
            }
            catch (ArgumentException)
            {
                return Process;
            }
        }
    }

    public ConsoleProcess(string appName, bool useShellExecute, bool redirect, bool openInNewWindow = false)
    {
        if (openInNewWindow && redirect)
        {
            throw new InvalidOperationException("Standard I/O cannot be redirected when opening in a new window.");
        }

        if (redirect && useShellExecute)
        {
            throw new InvalidOperationException("Can't redirect std in/out when useShellExecute is true");
        }

        if (OperatingSystem.IsLinux() && openInNewWindow)
        {
            bool hasDisplay = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DISPLAY")) ||
                !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WAYLAND_DISPLAY"));

            if (!hasDisplay || !IsLinuxTerminalAvailable())
            {
                Log.Debug("No terminal emulator is available, launching without a new window");
                openInNewWindow = false;
            }
        }

        _filePath = appName;
        IORedirected = redirect;
        OpenInNewWindow = openInNewWindow;

        Process.StartInfo = new ProcessStartInfo(appName)
        {
            RedirectStandardError = redirect,
            RedirectStandardInput = redirect,
            RedirectStandardOutput = redirect,
            UseShellExecute = useShellExecute,
            CreateNoWindow = !useShellExecute && !openInNewWindow,
        };

        Process.EnableRaisingEvents = true;

        Process.Exited += ProcessOnExited;
    }

    public async Task ExecuteAsync(string? workingDir, params string[] args)
    {
        if (running)
        {
            throw new InvalidOperationException("Process is still Running. Please wait for the process to complete.");
        }

        if (!string.IsNullOrEmpty(workingDir))
        {
            Process.StartInfo.WorkingDirectory = workingDir;
        }

        if (OpenInNewWindow)
        {
#if KEEP_WINDOW_OPEN
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Process.StartInfo.UseShellExecute = true;
                Process.StartInfo.FileName = "cmd.exe";
                Process.StartInfo.Arguments = $"/k \"\"{_filePath}\" {FormatStandardArguments(args)}\"";
            }
            else
            {
                ApplyTerminalWrapper(args);
            }
#else
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                ApplyTerminalWrapper(args);
            }
            else
            {
                Process.StartInfo.Arguments = FormatStandardArguments(args);
            }
#endif
        }
        else
        {
            Process.StartInfo.Arguments = FormatStandardArguments(args);
        }

        Process.Start();
        running = true;

        if (_pidFilePath != null)
        {
            _actualAppPid = await ResolveActualPidAsync(_pidFilePath);
        }

        if (IORedirected)
        {
            Process.BeginOutputReadLine();
            Process.BeginErrorReadLine();
        }
    }

    public void Write(string data)
    {
        if (!IORedirected)
        {
            throw new InvalidOperationException($"Can't write, because {nameof(IORedirected)} is false");
        }

        if (data is null)
        {
            return;
        }

        Process.StandardInput.Write(data);
        Process.StandardInput.Flush();
    }

    public void WriteLine(string data)
        => Write(data + Environment.NewLine);

    private static string FormatStandardArguments(IEnumerable<string> args)
    {
        var formattedArgs = args.Select(a =>
        {
            if (string.IsNullOrEmpty(a))
            {
                return "\"\"";
            }

            if (a.Contains(' ') || a.Contains('{') || a.Contains('"'))
            {
                return $"\"{a.Replace("\"", "\\\"")}\"";
            }

            return a;
        });

        return string.Join(" ", formattedArgs);
    }

    private void OnProcessExited()
        => ProcessExited?.Invoke(this, EventArgs.Empty);

    private void ProcessOnExited(object? sender, EventArgs eventArgs)
        => OnProcessExited();

    public async Task WaitForExitAsync(CancellationToken cancellationToken = default)
        => await ActualProcess.WaitForExitAsync(cancellationToken);

    public async Task StopNoWaitAsync(int timeout = 15 * 1000, CancellationToken cancellationToken = default)
        => await ActualProcess.StopGracefullyOrKillAsync(timeout, cancellationToken);

    public async Task StopAndWaitAsync(int timeout = 15 * 1000, CancellationToken cancellationToken = default)
        => await ActualProcess.StopGracefullyOrKillAndWaitAsync(timeout, cancellationToken);

    private void ApplyTerminalWrapper(IEnumerable<string> args)
    {
        Process.StartInfo.UseShellExecute = true;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            if (!IsLinuxTerminalAvailable())
            {
                throw new InvalidOperationException("No suitable Linux terminal emulator could be found.");
            }

            Process.StartInfo.FileName = _cachedLinuxTerminal;

            var linuxArgs = args.Select(a => $"'{a.Replace("'", "'\\''")}'");

            string innerCommand = $"'{_filePath.Replace("'", "'\\''")}' {string.Join(" ", linuxArgs)}";

            innerCommand = innerCommand
                .Replace("\\", "\\\\")
                .Replace("$", "\\$")
                .Replace("\"", "\\\"");

            _pidFilePath = Path.GetTempFileName();

#if KEEP_WINDOW_OPEN
            Process.StartInfo.Arguments = $"{_cachedLinuxTerminalExecArg} bash -c \"{innerCommand} & app_pid=$!; echo $app_pid > '{_pidFilePath}'; wait $app_pid; echo; echo '[Process completed - Press Enter to close]'; read\"";
#else
            Process.StartInfo.Arguments = $"{_cachedLinuxTerminalExecArg} bash -c \"echo $$ > '{_pidFilePath}'; exec {innerCommand}\"";
#endif
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            // todo: currently not tested
            string arguments = FormatStandardArguments(args);
            string command = $"'{_filePath}' {arguments}";

#if KEEP_WINDOW_OPEN
            string appleScript = $"tell application \"Terminal\" to do script \"{command.Replace("\"", "\\\"")}\"";
#else
            string appleScript = $"tell application \"Terminal\" to do script \"{command.Replace("\"", "\\\"")}; exit\"";
#endif

            Process.StartInfo.FileName = "osascript";
            Process.StartInfo.Arguments = $"-e \"{appleScript}\"";
        }
        else
        {
            Debug.Fail("Unsupported platform");
        }
    }

    [MemberNotNullWhen(true, nameof(_cachedLinuxTerminal), nameof(_cachedLinuxTerminalExecArg))]
    private static bool IsLinuxTerminalAvailable()
    {
        lock (_terminalCacheLock)
        {
            if (_linuxTerminalDiscoveryAttempted)
            {
                return _cachedLinuxTerminal != null;
            }

            _linuxTerminalDiscoveryAttempted = true;

            foreach (var (name, executionArg) in _linuxTerminalsToCheck)
            {
                try
                {
                    using var process = new Process
                    {
                        StartInfo = new ProcessStartInfo
                        {
                            FileName = "/bin/sh",
                            Arguments = $"-c \"command -v {name}\"",
                            RedirectStandardOutput = true,
                            UseShellExecute = false,
                            CreateNoWindow = true
                        }
                    };

                    process.Start();
                    process.WaitForExit();

                    if (process.ExitCode == 0)
                    {
                        Log.Information($"Using '{name}' to launch new terminal windows.");
                        _cachedLinuxTerminal = name;
                        _cachedLinuxTerminalExecArg = executionArg;
                        return true;
                    }
                }
                catch
                {
                }
            }

            return false;
        }
    }

    private static async Task<int?> ResolveActualPidAsync(string pidFile, int timeout = 5000)
    {
        using var cts = new CancellationTokenSource(timeout);

        try
        {
            while (!cts.IsCancellationRequested)
            {
                try
                {
                    if (File.Exists(pidFile))
                    {
                        var content = await File.ReadAllTextAsync(pidFile, cts.Token);
                        if (int.TryParse(content.Trim(), out int pid))
                        {
                            File.Delete(pidFile);
                            return pid;
                        }
                    }
                }
                catch (IOException)
                {
                }

                await Task.Delay(100, cts.Token);
            }
        }
        catch (OperationCanceledException)
        {
            Log.Warning("Timed out waiting for terminal emulator to report its PID.");
        }
        finally
        {
            if (File.Exists(pidFile))
            {
                File.Delete(pidFile);
            }
        }

        return null;
    }

    public void Dispose()
    {
        Process.Dispose();
        _cachedActualProcess?.Dispose();
    }
}