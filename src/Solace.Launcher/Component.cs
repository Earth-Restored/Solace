using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Solace.Common.Utils;

namespace Solace.Launcher;

internal sealed class Component : IAsyncDisposable
{
    private readonly string _command;

    private readonly bool _useShellExecute;
    private readonly bool _redirectOutput = true;
    private readonly DirectoryInfo? _workingDirectory;

    private readonly IReadOnlyCollection<string> _arguments;

    private readonly IReadOnlyDictionary<string, string?> _environmentVariables;

    private Process? _process;

    private Component(string command, bool useShellExecute, bool redirectOutput, DirectoryInfo? workingDirectory, IReadOnlyCollection<string> arguments, IReadOnlyDictionary<string, string?> environmentVariables)
    {
        _command = command;
        _useShellExecute = useShellExecute;
        _redirectOutput = redirectOutput;
        _arguments = arguments;
        _environmentVariables = environmentVariables;
        _workingDirectory = workingDirectory;
    }

    [MemberNotNullWhen(true, nameof(_process))]
    public bool IsRunning => _process is not null && !_process.HasExited;

    public async Task StartAsync()
    {
        if (IsRunning)
        {
            return;
        }

        _process?.Dispose();

        var startInfo = new ProcessStartInfo(_command, _arguments)
        {
            RedirectStandardOutput = _redirectOutput,
            RedirectStandardError = _redirectOutput,
            RedirectStandardInput = false,
            UseShellExecute = _useShellExecute,
            CreateNoWindow = true,
        };

        if (_workingDirectory is not null)
        {
            startInfo.WorkingDirectory = _workingDirectory.FullName;
        }

        foreach (var item in _environmentVariables)
        {
            startInfo.Environment[item.Key] = item.Value;
        }

        _process = new Process
        {
            StartInfo = startInfo,
            EnableRaisingEvents = true,
        };

        _process.Start();
    }

    public async Task StopAsync()
    {
        if (IsRunning)
        {
            await _process.StopGracefullyOrKillAsync(200, NullLogger.Instance, default);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_process is not null)
        {
            await _process.StopGracefullyOrKillAsync(200, NullLogger.Instance, default);
            _process.Dispose();
        }
    }

    public sealed class Builder
    {
        private readonly string _command;

        private bool _useShellExecute;
        private bool _redirectOutput = true;
        private DirectoryInfo? _workingDirectory;

        private readonly IReadOnlyCollection<string> _arguments;

        private readonly Dictionary<string, string?> _environmentVariables = [];

        private Builder(string command, IReadOnlyCollection<string> arguments)
        {
            _command = command;
            _arguments = arguments;
        }

        public static Builder Executable(FileInfo executableFile, IReadOnlyCollection<string> arguments)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                executableFile = new FileInfo(executableFile.FullName + ".exe");
            }

            return new Builder(executableFile.FullName, arguments)
                .WithWorkingDirectory(executableFile.Directory!);
        }

        public static Builder Command(string command, IReadOnlyCollection<string> arguments)
            => new Builder(command, arguments)
                .UseShellExecute(true)
                .RedirectOutput(false);

        public Builder UseShellExecute(bool useShellExecute)
        {
            _useShellExecute = useShellExecute;
            return this;
        }

        public Builder RedirectOutput(bool redirectOutput)
        {
            _redirectOutput = redirectOutput;
            return this;
        }

        public Builder WithWorkingDirectory(DirectoryInfo workingDirectory)
        {
            _workingDirectory = workingDirectory;
            return this;
        }

        public Builder WithEnvironmentVariable(string name, string value)
        {
            _environmentVariables[name.Replace(":", "__", StringComparison.Ordinal)] = value;
            return this;
        }

        public Builder WithEnvironmentFromSection(IConfiguration config, string sectionPath, string prefixToRemove = "")
        {
            var section = config.GetSection(sectionPath);

            foreach (var kvp in section.AsEnumerable())
            {
                if (kvp.Value is null)
                {
                    continue;
                }

                var envName = kvp.Key;
                if (!string.IsNullOrEmpty(prefixToRemove) && envName.StartsWith(prefixToRemove, StringComparison.Ordinal))
                {
                    envName = envName[prefixToRemove.Length..];
                }

                WithEnvironmentVariable(envName, kvp.Value);
            }

            return this;
        }

        public Builder WithEndpoint(string name, int value)
        {
            _environmentVariables[name] = value.ToString(CultureInfo.InvariantCulture);
            return this;
        }

        public Builder WithHttpEndpoint(int port)
        {
            WithEnvironmentVariable("ASPNETCORE_URLS", $"http://*:{port}");
            return this;
        }

        public Builder WithEndpointReference(string serviceName, string name, string scheme, int port)
        {
            WithEnvironmentVariable($"services__{serviceName}__{name}__0", $"{scheme}://localhost:{port}");
            return this;
        }

        public Builder WithOtel(string serviceName, string otlpEndpoint, string otlpApiKey)
        {
            WithEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT", otlpEndpoint);
            WithEnvironmentVariable("OTEL_SERVICE_NAME", serviceName);
            WithEnvironmentVariable("OTEL_BLRP_SCHEDULE_DELAY", "1000");
            WithEnvironmentVariable("OTEL_BSP_SCHEDULE_DELAY", "1000");
            WithEnvironmentVariable("OTEL_EXPORTER_OTLP_PROTOCOL", "grpc");
            WithEnvironmentVariable("OTEL_METRICS_EXEMPLAR_FILTER", "trace_based");
            WithEnvironmentVariable("OTEL_METRIC_EXPORT_INTERVAL", "1000");
            WithEnvironmentVariable("OTEL_TRACES_SAMPLER", "always_on");
            WithEnvironmentVariable("OTEL_EXPORTER_OTLP_HEADERS", $"x-otlp-api-key={otlpApiKey}");
            return this;
        }

        public Component Build()
            => new Component(_command, _useShellExecute, _redirectOutput, _workingDirectory, _arguments, _environmentVariables);
    }
}