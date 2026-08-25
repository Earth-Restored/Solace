using System.Buffers;
using System.Diagnostics;
using System.IO.Compression;
using System.Reflection;
using System.Runtime.Versioning;
using System.Text.Json.Serialization;
using BitcoderCZ.IO;
using Cyotek.Data.Nbt;
using Cyotek.Data.Nbt.Serialization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Solace.Buildplate.Common;
using Solace.Common;
using Solace.Common.Utils;
using static Solace.Buildplate.Common.Constants;

namespace Solace.Buildplate.ServerSetup;

internal sealed partial class SetupService : IDisposable
{
    private static readonly (string Name, string Pattern)[] Mods =
    [
        ("fabric-api", "fabric-api-*.jar"),
        ("ferrite-core", "ferritecore-*-fabric.jar"),
        ("lithium", "lithium-fabric-mc*.jar"),
        ("modernfix", "modernfix-fabric-*.jar"),
    ];

    // todo: configurable
    private static readonly TimeSpan StartTimeout = TimeSpan.FromMinutes(30); // needs to download files, user could have really slow internet
    private static readonly TimeSpan StopTimeout = TimeSpan.FromSeconds(15);

    // todo: configurable
    // todo: optimize
    private static readonly string[] JavaOptions = ["-Xms256M", "-Xmx1G", "-XX:+UseG1GC", "-XX:+ParallelRefProcEnabled", "-XX:MaxGCPauseMillis=200", "-XX:+UnlockExperimentalVMOptions", "-XX:+DisableExplicitGC", "-XX:G1NewSizePercent=20", "-XX:G1MaxNewSizePercent=30", "-XX:G1HeapRegionSize=4M", "-XX:G1ReservePercent=15", "-XX:G1HeapWastePercent=5", "-XX:G1MixedGCCountTarget=4", "-XX:InitiatingHeapOccupancyPercent=15", "-XX:G1MixedGCLiveThresholdPercent=90", "-XX:G1RSetUpdatingPauseTimePercent=5", "-XX:SurvivorRatio=32", "-XX:MaxTenuringThreshold=1", "-XX:+PerfDisableSharedMem", "-XX:MaxMetaspaceSize=192M", "-XX:MaxDirectMemorySize=128M", "-Xss256k"];

    private readonly HttpClient _httpClient;
    private readonly AbsoluteDirectory _serverDirectory;
    private readonly AbsoluteDirectory _modsDirectory;
    private readonly ILogger<SetupService> _logger;

    public SetupService(IConfiguration configuration, ILogger<SetupService> logger)
    {
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd($"Earth-Restored-Solace/{Assembly.GetExecutingAssembly().GetName().Version}");

        var staticDataPath = Path.GetFullPath(configuration["StaticDataPath"]!);

        _serverDirectory = new AbsoluteDirectory(Path.Combine(staticDataPath, "server_template_dir"));
        _modsDirectory = _serverDirectory.CreateSubdirectory("mods");

        _logger = logger;
    }

    [SupportedOSPlatform("android")]
    [SupportedOSPlatform("linux")]
    [SupportedOSPlatform("windows")]
    public async Task SetupAsync(CancellationToken cancellationToken = default)
    {
        var fileLock = new FileLock(_serverDirectory / new RelativeFile(".setupLock"));

        LogLockGetStart();
        FileLock.Handle lockHandle = default;
        Process? serverProcess = null;
        try
        {
            try
            {
                lockHandle = await fileLock.AcquireAsync(TimeSpan.FromSeconds(10), cancellationToken);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                LogLockTimeout();
                throw;
            }

            LogLockGetDone();

            var setupDoneFile = _serverDirectory / new RelativeFile(".setupDone");
            setupDoneFile.Delete();

            var serverJar = await SetupServer(cancellationToken);

            foreach (var (modName, modPattern) in Mods)
            {
                var modFile = _modsDirectory.EnumerateFiles(SearchOption.TopDirectoryOnly, modPattern).FirstOrDefault();

                if (modFile is not null)
                {
                    LogModFileFound(modFile.Value);
                    continue;
                }

                if (!await DownloadModAsync(modName, cancellationToken))
                {
                    return;
                }
            }

            // eula acceptance verified in Program.Main
            await (_serverDirectory / new RelativeFile("eula.txt")).WriteAllTextAsync("eula=true", cancellationToken);

            var preDownloadedFile = _serverDirectory / new RelativeFile(".preDownloadDone");

            if (preDownloadedFile.Exists &&
                (_serverDirectory / "libraries").Exists &&
                (_serverDirectory / "versions" / GameVersion.ToString()).Exists &&
                (_serverDirectory / ".fabric").Exists)
            {
                LogServerPreDownloaded();
            }
            else
            {
                LogServerPreDownloading();

                // dissables spawn region
                var worldDirectory = _serverDirectory.CreateSubdirectory("world");
                var levelDatTag = LevelDatUtils.Create(false, false, 0);
                using (var fs = new FileStream(Path.Combine(worldDirectory.Value, "level.dat"), FileMode.Create, FileAccess.Write, FileShare.Read))
                using (var gzs = new GZipStream(fs, CompressionLevel.Optimal))
                {
                    var writer = new BinaryTagWriter(gzs);
                    writer.WriteStartDocument();
                    writer.WriteStartTag(null, TagType.Compound);
                    writer.WriteTag(levelDatTag);
                    writer.WriteEndTag();
                    writer.WriteEndDocument();
                    writer.Close();
                }

                // copy solace mod out
                foreach (var solaceModFile in _modsDirectory.EnumerateFiles(SearchOption.TopDirectoryOnly, "solace-*.jar"))
                {
                    solaceModFile.MoveTo(_serverDirectory, true);
                }

                var javaExe = JavaLocator.Locate(_logger);

                serverProcess = new Process()
                {
                    StartInfo = new ProcessStartInfo(javaExe, [.. JavaOptions, "-jar", serverJar.Value, "--nogui"])
                    {
                        KillOnParentExit = true,
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardOutput = true,
                        RedirectStandardInput = true,
                        WorkingDirectory = _serverDirectory.Value,
                    },
                    EnableRaisingEvents = true,
                };

                var serverStarted = new TaskCompletionSource();
                serverProcess.OutputDataReceived += (sender, e) =>
                {
                    if (string.IsNullOrEmpty(e.Data))
                    {
                        return;
                    }

                    Console.WriteLine($"[server] {e.Data}");

                    if (GetServerStartedRegex().IsMatch(e.Data))
                    {
                        serverStarted.SetResult();
                    }
                };

                serverProcess.Exited += (sender, e) =>
                {
                    serverStarted.TrySetResult();
                };

                LogServerStarting();

                serverProcess.Start();

                serverProcess.BeginOutputReadLine();

                await serverStarted.Task.WaitAsync(StartTimeout, cancellationToken);
                if (serverProcess.HasExited)
                {
                    LogServerStoppedError(serverProcess.ExitCode);
                    return;
                }

                LogServerStarted();

                await serverProcess.StandardInput.WriteLineAsync("/stop");
                await serverProcess.StandardInput.FlushAsync(cancellationToken);

                await serverProcess.WaitForExitAsync(StopTimeout, cancellationToken);

                if (serverProcess.ExitCode != 0)
                {
                    LogServerStoppedError(serverProcess.ExitCode);
                    return;
                }

                LogServerStopped();

                preDownloadedFile.Create();
            }

            // copy solace mod in
            foreach (var solaceModFile in _serverDirectory.EnumerateFiles(SearchOption.TopDirectoryOnly, "solace-*.jar"))
            {
                solaceModFile.MoveTo(_modsDirectory, true);
            }

            // cleanup
            (_serverDirectory / "world").SafeDelete(recursive: true);
            (_serverDirectory / "logs").SafeDelete(recursive: true);
            (_serverDirectory / new RelativeFile("banned-ips.json")).Delete();
            (_serverDirectory / new RelativeFile("banned-players.json")).Delete();
            (_serverDirectory / new RelativeFile("eula.txt")).Delete();
            (_serverDirectory / new RelativeFile("ops.json")).Delete();
            (_serverDirectory / new RelativeFile("usercache.json")).Delete();
            (_serverDirectory / new RelativeFile("whitelist.json")).Delete();

            setupDoneFile.Create();

            LogSetupDone();
        }
        finally
        {
            lockHandle.Dispose();
            serverProcess?.Dispose();
        }
    }

    public void Dispose()
        => _httpClient.Dispose();

    private async Task<AbsoluteFile> SetupServer(CancellationToken cancellationToken)
    {
        if (File.TryFindCompatibleFile(_serverDirectory.Value, GameVersion, "server-{{version}}.jar", out var serverJarPath))
        {
            LogServerFileFound(serverJarPath);
            return new AbsoluteFile(serverJarPath);
        }

        // delete any incompatible files
        foreach (var file in _serverDirectory.EnumerateFiles(SearchOption.TopDirectoryOnly, "server-*.jar"))
        {
            try
            {
                file.Delete();
            }
            catch
            {
                // idc, TryFindCompatibleFile will use the new file anyway
            }
        }

        var serverFile = _serverDirectory / new RelativeFile($"server-{GameVersion}.jar");

        LogDownloadServerStart();

        await DownloadFileAsync($"https://meta.fabricmc.net/v2/versions/loader/{GameVersion}/0.19.3/1.1.2/server/jar", serverFile, cancellationToken);

        LogDownloadServerDone();

        return serverFile;
    }

    private async Task<bool> DownloadModAsync(string name, CancellationToken cancellationToken)
    {
        var versionsUrl = $"https://api.modrinth.com/v2/project/{name}/version?game_versions=[\"{GameVersion}\"]&loaders=[\"fabric\"]";
        var versionsResponse = await _httpClient.GetAsync(versionsUrl, cancellationToken);
        versionsResponse.EnsureSuccessStatusCode();

        var versions = await versionsResponse.Content.ReadFromJsonAsync(AppJsonContext.Default.ModrinthVersionArray, cancellationToken);

        if (versions is null or [])
        {
            LogModNoVersion(name, GameVersion);
            return false;
        }

        var latestVersion = versions[0];

        var primaryFile = latestVersion.Files.FirstOrDefault(file => file.Primary) ?? latestVersion.Files.FirstOrDefault(file => file.FileName.EndsWith(".jar", StringComparison.Ordinal));

        if (primaryFile is null)
        {
            LogModNoJar(name);
            return false;
        }

        LogModFoundVersion(name, latestVersion.VersionNumber);

        var destinationFile = _modsDirectory / new RelativeFile(primaryFile.FileName);

        await DownloadFileAsync(primaryFile.Url, destinationFile, cancellationToken);

        LogModDownloadDone(name, destinationFile.Value);

        return true;
    }

    private async Task DownloadFileAsync(string requestUri, AbsoluteFile destinationFile, CancellationToken cancellationToken)
    {
        const int BufferSize = 1024 * 8; // 8 kb

        using var response = await _httpClient.GetAsync(requestUri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

        response.EnsureSuccessStatusCode();

        var totalBytes = response.Content.Headers.ContentLength;

        using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var fileStream = new FileStream(destinationFile.Value, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize: BufferSize, useAsync: true);

        var buffer = ArrayPool<byte>.Shared.Rent(BufferSize);
        long totalBytesRead = 0;
        int bytesRead;
        var lastLoggedPercentage = -1;

        try
        {
            while ((bytesRead = await contentStream.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken)) > 0)
            {
                await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
                totalBytesRead += bytesRead;

                if (totalBytes.HasValue && totalBytes.Value > 0)
                {
                    var currentPercentage = (int)((double)totalBytesRead / totalBytes.Value * 10); // update every 10 percent

                    if (currentPercentage != lastLoggedPercentage)
                    {
                        LogDownloadProgressWithTotal(totalBytesRead, totalBytes.Value, currentPercentage * 10);

                        lastLoggedPercentage = currentPercentage;
                    }
                }
                else
                {
                    LogDownloadProgressWithoutTotal(totalBytesRead);
                }
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    [JsonNamingPolicy(JsonKnownNamingPolicy.SnakeCaseLower)]
    internal sealed class ModrinthVersion
    {
        public required string Id { get; init; }

        public required string VersionNumber { get; init; }

        public required string[] GameVersions { get; init; }

        public required ModrinthFile[] Files { get; init; }
    }

    [JsonNamingPolicy(JsonKnownNamingPolicy.CamelCase)]
    internal sealed class ModrinthFile
    {
        public required string Url { get; init; }

        [JsonPropertyName("filename")]
        public required string FileName { get; init; }

        public required bool Primary { get; init; }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Acquiring lock")]
    private partial void LogLockGetStart();

    [LoggerMessage(Level = LogLevel.Critical, Message = "Timeout while waiting for lock")]
    private partial void LogLockTimeout();

    [LoggerMessage(Level = LogLevel.Information, Message = "Acquired lock")]
    private partial void LogLockGetDone();

    [LoggerMessage(Level = LogLevel.Information, Message = "Found fabric server: {Path}")]
    private partial void LogServerFileFound(string Path);

    [LoggerMessage(Level = LogLevel.Information, Message = "Downloading fabric server")]
    private partial void LogDownloadServerStart();

    [LoggerMessage(Level = LogLevel.Information, Message = "Fabric server downloaded")]
    private partial void LogDownloadServerDone();

    [LoggerMessage(Level = LogLevel.Information, Message = "Found mod: {Path}")]
    private partial void LogModFileFound(string Path);

    [LoggerMessage(Level = LogLevel.Critical, Message = "No compatible version of {Name} found for Minecraft {MinecraftVersion}")]
    private partial void LogModNoVersion(string Name, Version MinecraftVersion);

    [LoggerMessage(Level = LogLevel.Critical, Message = "No jar file found for mod {Name}")]
    private partial void LogModNoJar(string Name);

    [LoggerMessage(Level = LogLevel.Information, Message = "Found version {Version} of mod {Name}, downloading")]
    private partial void LogModFoundVersion(string Name, string Version);

    [LoggerMessage(Level = LogLevel.Information, Message = "Downloaded mod {Name} to {Path}")]
    private partial void LogModDownloadDone(string Name, string Path);

    [LoggerMessage(Level = LogLevel.Information, Message = "Server files already pre downloaded")]
    private partial void LogServerPreDownloaded();

    [LoggerMessage(Level = LogLevel.Information, Message = "Running server to pre download files")]
    private partial void LogServerPreDownloading();

    [LoggerMessage(Level = LogLevel.Information, Message = "Server starting")]
    private partial void LogServerStarting();

    [LoggerMessage(Level = LogLevel.Information, Message = "Server started")]
    private partial void LogServerStarted();

    [LoggerMessage(Level = LogLevel.Information, Message = "Server stopped")]
    private partial void LogServerStopped();

    [LoggerMessage(Level = LogLevel.Critical, Message = "Server exited unexpectedly with code {Code}")]
    private partial void LogServerStoppedError(int Code);

    [LoggerMessage(Level = LogLevel.Information, Message = "Server setup finished")]
    private partial void LogSetupDone();

    [LoggerMessage(Level = LogLevel.Debug, Message = "Downloaded {BytesRead}/{TotalBytes} bytes {Percentage}%")]
    private partial void LogDownloadProgressWithTotal(long BytesRead, long TotalBytes, int Percentage);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Downloaded {BytesRead} bytes")]
    private partial void LogDownloadProgressWithoutTotal(long BytesRead);
}
