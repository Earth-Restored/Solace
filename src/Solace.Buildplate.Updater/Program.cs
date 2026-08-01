using System.Diagnostics;
using System.Globalization;
using System.IO.Compression;
using System.Text.RegularExpressions;
using Cyotek.Data.Nbt;
using Cyotek.Data.Nbt.Serialization;
using Microsoft.Extensions.Logging.Abstractions;
using Solace.Buildplate.Model;
using Solace.Common.Utils;

namespace Solace.Buildplate.Updater;

internal static class Program
{
    private static async Task Main()
    {
        var serverFolder = new DirectoryInfo("world");
        var worldFolder = new DirectoryInfo(Path.Combine(serverFolder.FullName, "world"));

        worldFolder.Delete(true);
        worldFolder.Create();

        await ZipFile.ExtractToDirectoryAsync("bp.zip", worldFolder.FullName);

        var metadata = WorldData.LoadMetadata(await File.ReadAllTextAsync(Path.Combine(worldFolder.FullName, "buildplate_metadata.json")), NullLogger.Instance)!;

        var levelDatTag = CreateLevelDat(false, false);
        using (var fs = new FileStream(Path.Combine(worldFolder.FullName, "level.dat"), FileMode.OpenOrCreate, FileAccess.Write, FileShare.Read))
        using (var gzs = new GZipStream(fs, CompressionLevel.Optimal))
        {
            var writer = new BinaryTagWriter(gzs);
            writer.WriteStartDocument();
            writer.WriteStartTag(null, TagType.Compound);
            writer.WriteTag(levelDatTag);
            writer.WriteEndTag();
            writer.WriteEndDocument();
        }

        // todo: configurable
        // todo: cleanup
        string[] javaOptions = ["-Xms256M", "-Xmx1G", "-XX:+UseG1GC", "-XX:+ParallelRefProcEnabled", "-XX:MaxGCPauseMillis=200", "-XX:+UnlockExperimentalVMOptions", "-XX:+DisableExplicitGC", "-XX:G1NewSizePercent=20", "-XX:G1MaxNewSizePercent=30", "-XX:G1HeapRegionSize=4M", "-XX:G1ReservePercent=15", "-XX:G1HeapWastePercent=5", "-XX:G1MixedGCCountTarget=4", "-XX:InitiatingHeapOccupancyPercent=15", "-XX:G1MixedGCLiveThresholdPercent=90", "-XX:G1RSetUpdatingPauseTimePercent=5", "-XX:SurvivorRatio=32", "-XX:MaxTenuringThreshold=1", "-XX:+PerfDisableSharedMem", "-XX:MaxMetaspaceSize=192M", "-XX:MaxDirectMemorySize=128M", "-Xss256k"];

        var server = new Process()
        {
            StartInfo = new ProcessStartInfo("java", [.. javaOptions, "-jar", Path.Combine(serverFolder.FullName, "server.jar"), "--nogui"])
            {
                KillOnParentExit = true,
                RedirectStandardOutput = true,
                RedirectStandardInput = true,
                WorkingDirectory = serverFolder.FullName,
            },
        };

        server.Start();

        var serverStarted = new TaskCompletionSource();
        var chunksForceLoaded = new TaskCompletionSource();
        server.OutputDataReceived += (sender, e) =>
        {
            if (string.IsNullOrEmpty(e.Data))
            {
                return;
            }

            Console.WriteLine($"[java] {e.Data}");

            if (Regex.IsMatch(e.Data, @"Done \((.*?)\)! For help, type ""help"""))
            {
                serverStarted.SetResult();
            }

            var forceLoadMarkedMatch = Regex.Match(e.Data, @"Marked \d+ chunks in [\w:]+ from \[(?<x1>-?\d+),\s*(?<y1>-?\d+)\] to \[(?<x2>-?\d+),\s*(?<y2>-?\d+)\] to be force loaded");

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

        server.BeginOutputReadLine();

        await serverStarted.Task;

        Console.WriteLine("Server started");

        await Task.Delay(100);

        await server.StandardInput.WriteLineAsync($"/forceload add {-metadata.Size} {-metadata.Size} {metadata.Size} {metadata.Size}");

        await chunksForceLoaded.Task;

        await Task.Delay(500);

        await server.StandardInput.WriteLineAsync($"/stop");

        await server.WaitForExitAsync();

        File.Delete("bp-out.zip");
        await using var resultFs = File.OpenWriteNew("bp-out.zip");
        using var result = new ZipArchive(resultFs, ZipArchiveMode.Create);

        await result.CreateEntryFromFileAsync(Path.Combine(worldFolder.FullName, "buildplate_metadata.json"), "buildplate_metadata.json");

        foreach (var file in Directory.EnumerateFiles(Path.Combine(worldFolder.FullName, "region")))
        {
            await result.CreateEntryFromFileAsync(file, $"region/{Path.GetFileName(file)}");
        }

        foreach (var file in Directory.EnumerateFiles(Path.Combine(worldFolder.FullName, "entities")))
        {
            await result.CreateEntryFromFileAsync(file, $"entities/{Path.GetFileName(file)}");
        }
    }

    private static TagCompound CreateLevelDat(bool survival, bool night)
    {
        var dataTag = new NbtBuilder.Compound()
            .Add("GameType", survival ? 0 : 1)
            .Add("Difficulty", 1)
            .Add("DayTime", !night ? 6000 : 18000)
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
}