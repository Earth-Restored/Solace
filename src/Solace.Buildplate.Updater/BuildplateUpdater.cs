using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO.Compression;
using System.Runtime.Versioning;
using System.Text.RegularExpressions;
using Cyotek.Data.Nbt;
using Cyotek.Data.Nbt.Serialization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Solace.Buildplate.Model;
using Solace.Common;
using Solace.Common.Utils;

namespace Solace.Buildplate.Updater;

internal sealed partial class BuildplateUpdater
{
    // todo: configurable
    private static readonly string[] Mods = ["fabric-api-*.jar", "ferritecore-*-fabric.jar", "fountain-*.jar", "lithium-fabric-mc*.jar", "modernfix-fabric-*.jar"];

    // todo: configurable
    // todo: optimize
    private static readonly string[] JavaOptions = ["-Xms256M", "-Xmx1G", "-XX:+UseG1GC", "-XX:+ParallelRefProcEnabled", "-XX:MaxGCPauseMillis=200", "-XX:+UnlockExperimentalVMOptions", "-XX:+DisableExplicitGC", "-XX:G1NewSizePercent=20", "-XX:G1MaxNewSizePercent=30", "-XX:G1HeapRegionSize=4M", "-XX:G1ReservePercent=15", "-XX:G1HeapWastePercent=5", "-XX:G1MixedGCCountTarget=4", "-XX:InitiatingHeapOccupancyPercent=15", "-XX:G1MixedGCLiveThresholdPercent=90", "-XX:G1RSetUpdatingPauseTimePercent=5", "-XX:SurvivorRatio=32", "-XX:MaxTenuringThreshold=1", "-XX:+PerfDisableSharedMem", "-XX:MaxMetaspaceSize=192M", "-XX:MaxDirectMemorySize=128M", "-Xss256k"];

    // todo: configurable
    private static readonly TimeSpan StartTimeout = TimeSpan.FromSeconds(120);
    private static readonly TimeSpan LoadTimeout = TimeSpan.FromSeconds(20);
    private static readonly TimeSpan StopTimeout = TimeSpan.FromSeconds(15);

    private readonly SemaphoreSlim _convertLock = new(1, 1);

    private readonly string _staticDataPath;

    private readonly ILogger<BuildplateUpdater> _logger;

    private DirectoryInfo? _serverDirectory;

    private DirectoryInfo? _worldDirectory;

    private FileInfo? _serverJar;

    private FileInfo? _templateLevelDat;

    private FileInfo? _levelDat;

    private string? _javaExe;

    public BuildplateUpdater(IConfiguration configuration, ILogger<BuildplateUpdater> logger)
    {
        _staticDataPath = configuration["StaticDataPath"]!;
        _logger = logger;
    }

    [MemberNotNullWhen(true, nameof(_serverDirectory), nameof(_worldDirectory), nameof(_serverJar), nameof(_templateLevelDat), nameof(_levelDat), nameof(_javaExe))]
    private bool Initialized { get; set; }

    public async Task<bool> InitializeAsync(CancellationToken cancellationToken = default)
    {
        _ = cancellationToken;

        var tempDirectory = new DirectoryInfo("tmp");
        if (tempDirectory.Exists)
        {
            tempDirectory.Delete(recursive: true);
        }

        tempDirectory.Create();

        _serverDirectory = tempDirectory.CreateSubdirectory("server");

        // eula acceptance verified in Program.Main
        File.WriteAllText(Path.Combine(_serverDirectory.FullName, "eula.txt"), "eula=true");

        var staticDataServer = new DirectoryInfo(Path.Combine(_staticDataPath, "server_template_dir"));

        if (!File.TryFindCompatibleFile(staticDataServer.FullName, new Version(1, 20, 5, 0), "server-{{version}}.jar", out var serverJarPath))
        {
            LogServerJarNotFound();
            return false;
        }

        _serverJar = new FileInfo(Path.Combine(_serverDirectory.FullName, Path.GetFileName(serverJarPath)));

        File.Copy(serverJarPath, _serverJar.FullName, true);

        var modsDirectory = _serverDirectory.CreateSubdirectory("mods");

        new DirectoryInfo(Path.Combine(staticDataServer.FullName, "mods")).CopyFilesTo(modsDirectory.FullName, Mods, overwrite: false);

        _worldDirectory = _serverDirectory.CreateSubdirectory("world");

        // same for all buildplate, create once and copy
        _templateLevelDat = new FileInfo(Path.Combine(_serverDirectory.FullName, "level.dat"));
        _levelDat = new FileInfo(Path.Combine(_worldDirectory.FullName, "level.dat"));

        var levelDatTag = CreateLevelDat();
        using (var fs = new FileStream(_templateLevelDat.FullName, FileMode.Create, FileAccess.Write, FileShare.Read))
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

        _javaExe = JavaLocator.Locate(_logger);

        Initialized = true;

        return true;
    }

    [SupportedOSPlatform("android")]
    [SupportedOSPlatform("linux")]
    [SupportedOSPlatform("windows")]
    public async Task<byte[]?> UpdateAsync(Stream worldZipData, CancellationToken cancellationToken = default)
    {
        if (!Initialized)
        {
            throw new InvalidOperationException($"{nameof(BuildplateUpdater)} needs to be initialized.");
        }

        // todo: detect if any updates are queued, and if so, reuse the server - https://modrinth.com/mod/multiworld

        // convert only 1 buildplate at a time, don't want multiple java server instances
        // not worth it to keep 1 server running and update in multiple dimensions, since this runs only at first startup and when user imports a buildplate

        await _convertLock.WaitAsync(cancellationToken);

        try
        {
            _worldDirectory.Delete(true);
            _worldDirectory.Create();

            using (var worldZip = new ZipArchive(worldZipData, ZipArchiveMode.Read, leaveOpen: true))
            {
                await worldZip.ExtractToDirectoryAsync(_worldDirectory.FullName, cancellationToken);
            }

            var metadata = WorldData.LoadMetadata(await File.ReadAllTextAsync(Path.Combine(_worldDirectory.FullName, "buildplate_metadata.json"), cancellationToken), _logger);

            if (metadata is null)
            {
                return null;
            }

            _templateLevelDat.CopyTo(_levelDat.FullName, true);

            var serverProcess = new Process()
            {
                StartInfo = new ProcessStartInfo(_javaExe, [.. JavaOptions, "-jar", _serverJar.FullName, "--nogui"])
                {
                    KillOnParentExit = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardInput = true,
                    WorkingDirectory = _serverDirectory.FullName,
                },
            };

            var serverStarted = new TaskCompletionSource();
            var chunksForceLoaded = new TaskCompletionSource();
            serverProcess.OutputDataReceived += (sender, e) =>
            {
                if (string.IsNullOrEmpty(e.Data))
                {
                    return;
                }

                // Console.WriteLine($"[java] {e.Data}");

                if (GetServerStartedRegex().IsMatch(e.Data))
                {
                    serverStarted.SetResult();
                }

                var forceLoadMarkedMatch = GetChunksLoadedRegex().Match(e.Data);

                if (forceLoadMarkedMatch.Success)
                {
                    var x1 = int.Parse(forceLoadMarkedMatch.Groups["x1"].Value, CultureInfo.InvariantCulture);
                    var y1 = int.Parse(forceLoadMarkedMatch.Groups["y1"].Value, CultureInfo.InvariantCulture);
                    var x2 = int.Parse(forceLoadMarkedMatch.Groups["x2"].Value, CultureInfo.InvariantCulture);
                    var y2 = int.Parse(forceLoadMarkedMatch.Groups["y2"].Value, CultureInfo.InvariantCulture);

                    var minChunk = -metadata.Size >> 4;
                    var maxChunk = metadata.Size >> 4;

                    if (x1 == minChunk && y1 == minChunk && x2 == maxChunk && y2 == maxChunk)
                    {
                        chunksForceLoaded.TrySetResult();
                    }
                }
            };

            LogServerStarting();

            serverProcess.Start();

            serverProcess.BeginOutputReadLine();

            await serverStarted.Task.WaitAsync(StartTimeout, cancellationToken);

            LogServerStarted();

            await Task.Delay(500, cancellationToken);

            await SendCommandAsync($"/forceload add {-metadata.Size} {-metadata.Size} {metadata.Size} {metadata.Size}");

            await chunksForceLoaded.Task.WaitAsync(LoadTimeout, cancellationToken);

            LogChunksLoaded();

            await Task.Delay(1000, cancellationToken);

            await SendCommandAsync("/stop");

            await serverProcess.WaitForExitAsync(StopTimeout, cancellationToken);

            LogServerStopped();

            using var resultFs = new MemoryStream();
            using (var result = new ZipArchive(resultFs, ZipArchiveMode.Create, leaveOpen: true))
            {

                await result.CreateEntryFromFileAsync(Path.Combine(_worldDirectory.FullName, "buildplate_metadata.json"), "buildplate_metadata.json", cancellationToken);

                foreach (var file in Directory.EnumerateFiles(Path.Combine(_worldDirectory.FullName, "region")))
                {
                    await result.CreateEntryFromFileAsync(file, $"region/{Path.GetFileName(file)}", cancellationToken);
                }

                foreach (var file in Directory.EnumerateFiles(Path.Combine(_worldDirectory.FullName, "entities")))
                {
                    await result.CreateEntryFromFileAsync(file, $"entities/{Path.GetFileName(file)}", cancellationToken);
                }
            }

            return resultFs.ToArray();

            async Task SendCommandAsync(string command)
            {
                await serverProcess.StandardInput.WriteLineAsync(command);
                await serverProcess.StandardInput.FlushAsync(cancellationToken);
            }
        }
        finally
        {
            _convertLock.Release();
        }
    }

    private static TagCompound CreateLevelDat()
    {
        var dataTag = new NbtBuilder.Compound()
            .Add("GameType", 1)
            .Add("Difficulty", 1)
            .Add("DayTime", 6000)
            .Add("GameRules", new NbtBuilder.Compound()
                .Add("doDaylightCycle", "false")
                .Add("doWeatherCycle", "false")
                .Add("doMobSpawning", "false")
                .Add("fountain:doMobDespawn", "false")
                .Add("keepInventory", "true")
                .Add("spawnChunkRadius", "0")
            )
            .Add("WorldGenSettings", new NbtBuilder.Compound()
                .Add("seed", (long)0)    // TODO
                .Add("generate_features", (byte)0)
                .Add("dimensions", new NbtBuilder.Compound()
                    .Add("minecraft:overworld", new NbtBuilder.Compound()
                        .Add("type", "minecraft:overworld")
                        .Add("generator", new NbtBuilder.Compound()
                            .Add("type", "fountain:wrapper")
                            .Add("buildplate", new NbtBuilder.Compound()
                                .Add("ground_level", 63))
                            .Add("inner", new NbtBuilder.Compound()
                                .Add("type", "minecraft:noise")
                                .Add("settings", "minecraft:overworld")
                                .Add("biome_source", new NbtBuilder.Compound()
                                    .Add("type", "minecraft:multi_noise")
                                    .Add("preset", "minecraft:overworld")
                                )
                            )
                        )
                    )
                    .Add("minecraft:the_nether", new NbtBuilder.Compound()
                        .Add("type", "minecraft:the_nether")
                        .Add("generator", new NbtBuilder.Compound()
                            .Add("type", "fountain:wrapper")
                            .Add("buildplate", new NbtBuilder.Compound()
                                .Add("ground_level", 32))
                            .Add("inner", new NbtBuilder.Compound()
                                .Add("type", "minecraft:noise")
                                .Add("settings", "minecraft:nether")
                                .Add("biome_source", new NbtBuilder.Compound()
                                    .Add("type", "minecraft:fixed")
                                    .Add("biome", "minecraft:nether_wastes")
                                )
                            )
                        )
                    )
                )
            )
            .Add("DataVersion", 3837)
            .Add("version", 19133)
            .Add("Version", new NbtBuilder.Compound()
                .Add("Id", 3837)
                .Add("Name", "1.20.5")
                .Add("Series", "main")
                .Add("Snapshot", (byte)0)
            )
            .Add("initialized", (byte)1)
            .Build("Data");

        return dataTag;
    }

    [GeneratedRegex(@"Done \((?<time>.*?)\)! For help, type ""help""", RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 200)]
    private static partial Regex GetServerStartedRegex();

    [GeneratedRegex(@"Marked \d+ chunks in [\w:]+ from \[(?<x1>-?\d+),\s*(?<y1>-?\d+)\] to \[(?<x2>-?\d+),\s*(?<y2>-?\d+)\] to be force loaded", RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 200)]
    private static partial Regex GetChunksLoadedRegex();

    [LoggerMessage(Level = LogLevel.Critical, Message = "Server jar not found")]
    private partial void LogServerJarNotFound();

    [LoggerMessage(Level = LogLevel.Information, Message = "Server starting")]
    private partial void LogServerStarting();

    [LoggerMessage(Level = LogLevel.Information, Message = "Server started")]
    private partial void LogServerStarted();

    [LoggerMessage(Level = LogLevel.Information, Message = "Chunks loaded")]
    private partial void LogChunksLoaded();

    [LoggerMessage(Level = LogLevel.Information, Message = "Server stopped")]
    private partial void LogServerStopped();
}
