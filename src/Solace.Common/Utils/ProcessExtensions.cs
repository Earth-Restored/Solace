using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;

namespace Solace.Common.Utils;

public static partial class ProcessExtensions
{
    extension(Process process)
    {
        public async Task StopGracefullyOrKillAsync(int timeout, ILogger logger, CancellationToken cancellationToken)
        {
            if (!await process.TryStopGracefullyAsync(timeout, logger, cancellationToken))
            {
                process.Kill(true);
            }
        }

        public async Task StopGracefullyOrKillAndWaitAsync(int timeout, ILogger logger, CancellationToken cancellationToken)
        {
            await process.StopGracefullyOrKillAsync(timeout, logger, cancellationToken);

            await process.WaitForExitAsync(timeout, cancellationToken);
        }

        public async Task<bool> TryStopGracefullyAsync(int timeout, ILogger logger, CancellationToken cancellationToken)
        {
            try
            {
                if (process.HasExited)
                {
                    return true;
                }
            }
            catch (InvalidOperationException)
            {
                // no associated process
                return true;
            }

            try
            {

                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    if (await process.WinTrySendCtrlCAsync(timeout, logger, cancellationToken))
                    {
                        return true;
                    }

                    if (await process.TryCloseMainWindowAsync(timeout, cancellationToken))
                    {
                        return true;
                    }
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) || RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    if (await process.UnixTrySendShutdownSignalAsync(timeout, cancellationToken))
                    {
                        return true;
                    }
                }
            }
            catch
            {
            }

            return process.HasExited;
        }

        public Task WaitForExitAsync(int timeout, CancellationToken cancellationToken)
            => Task.WhenAny(process.WaitForExitAsync(cancellationToken), Task.Delay(timeout, cancellationToken));

        #region Async
        private async Task<bool> WinTrySendCtrlCAsync(int timeout, ILogger logger, CancellationToken cancellationToken)
        {
            Debug.Assert(RuntimeInformation.IsOSPlatform(OSPlatform.Windows));

            var exePath = Path.GetFullPath("Solace.KillHelper.exe");

            var startInfo = new ProcessStartInfo(exePath, [process.Id.ToString(CultureInfo.InvariantCulture)])
            {
                UseShellExecute = true,
                CreateNoWindow = false
            };

            using (var killProcess = Process.Start(startInfo))
            {
                if (killProcess is null)
                {
                    LogKillerProcessStartFail(logger);
                    return false;
                }

                await killProcess.WaitForExitAsync(cancellationToken);
                var exitCode = killProcess.ExitCode;

                if (exitCode is 0)
                {
                    await process.WaitForExitAsync(timeout, cancellationToken);
                    return process.HasExited;
                }

                LogKillerProcessExitFail(logger, exitCode);

                return false;
            }
        }

        private async Task<bool> UnixTrySendShutdownSignalAsync(int timeout, CancellationToken cancellationToken)
        {
            try
            {
                var signal = await process.UnixGetSignalAsync(cancellationToken);

                var killProc = Process.Start("kill", $"-s {signal} {process.Id}");
                await killProc.WaitForExitAsync(1000, cancellationToken);
                Debug.Assert(killProc.HasExited);

                await process.WaitForExitAsync(timeout, cancellationToken);
            }
            catch
            {
            }

            return process.HasExited;
        }

        private async Task<string> UnixGetSignalAsync(CancellationToken cancellationToken)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                try
                {
                    // We want to see WHERE the symlink points, not read its contents.
                    var linkInfo = File.ResolveLinkTarget($"/proc/{process.Id}/fd/0", returnFinalTarget: true);
                    var targetPath = linkInfo?.FullName ?? string.Empty;

                    if (targetPath.Contains("/dev/tty", StringComparison.Ordinal) || targetPath.Contains("/dev/pts", StringComparison.Ordinal))
                    {
                        return "INT";
                    }
                }
                catch { }
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "ps",
                    Arguments = $"-o tty= -p {process.Id}",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var ps = Process.Start(psi);
                if (ps is not null)
                {
                    var tty = await ps.StandardOutput.ReadToEndAsync(cancellationToken);
                    await ps.WaitForExitAsync(cancellationToken);

                    if (!string.IsNullOrWhiteSpace(tty) && !tty.Contains('?', StringComparison.Ordinal))
                    {
                        return "INT";
                    }
                }
            }

            return "TERM";
        }

        private async Task<bool> TryCloseMainWindowAsync(int timeout, CancellationToken cancellationToken)
        {
            try
            {
                if (!process.CloseMainWindow())
                {
                    return false;
                }

                await process.WaitForExitAsync(timeout, cancellationToken);
            }
            catch
            {
            }

            return process.HasExited;
        }
        #endregion
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to start killer process")]
    private static partial void LogKillerProcessStartFail(ILogger logger);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Killer process exited with code {ExitCode}")]
    private static partial void LogKillerProcessExitFail(ILogger logger, int ExitCode);
}
