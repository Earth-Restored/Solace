using Serilog;
using System.Diagnostics;
using System.Reflection;
using Solace.Common;
using Solace.Common.Utils;
using Solace.LauncherUI.Programs;
using ILogger = Serilog.ILogger;

namespace Solace.LauncherUI;

internal static class FileChecker
{
    private static readonly HttpClient httpClient = new();

    private static readonly string[] expectedStaticFiles =
    [
        "catalog/itemEfficiencyCategories.json",
        "catalog/itemJournalGroups.json",
        "catalog/items.json",
        "catalog/nfc.json",
        "catalog/recipes.json",
        "catalog/recipes.json",
        "tile_renderer/tagMap1.json",
        "tile_renderer/tagMap2.json",
    ];

    private static readonly (string Template, Version MinimumVersion)[] expectedVersionedStaticFiles =
    [
        ("server_jars/buildplate-connector-plugin-{{version}}-SNAPSHOT-jar-with-dependencies.jar", BuildplateLauncher.MinimumBuildplateConnectorPluginVersion),
        ("server_jars/fountain-{{version}}-SNAPSHOT-jar-with-dependencies.jar", BuildplateLauncher.MinimumFountainBridgeVersion),
        ("server_template_dir/mods/fountain-{{version}}.jar", new Version(0, 0, 2)),
        ("server_template_dir/mods/vienna-{{version}}.jar", new Version(0, 0, 1)),
    ];

    private static readonly string[] expectedStaticDirectories = [
        "catalog",
        "encounters",
        "levels",
        "resourcepacks",
        "server_jars",
        "server_template_dir",
        "server_template_dir/mods",
        "tappables",
        "tile_renderer",
    ];

    static FileChecker()
    {
        bool added = httpClient.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", $"BitcoderCZ/Solace/{Assembly.GetExecutingAssembly().GetName().Version}");
        Debug.Assert(added);
    }

    public static async Task<bool> CheckAsync(Settings settings, bool checkImporter, ILogger logger, CancellationToken cancellationToken)
    {
        if (settings.SkipFileChecks is not true)
        {
            logger.Information("Validating files");
        }
        else
        {
            logger.Warning("Skipped file validation, you can turn it back on in 'Configure/Skip file validation before starting'");
            return true;
        }

        bool error = false;
        if (!EventBusServer.Check(settings, logger) ||
            !ObjectStoreServer.Check(settings, logger) ||
            !ApiServer.Check(settings, logger) ||
            !BuildplateLauncher.Check(settings, logger) ||
            !TappablesGenerator.Check(settings, logger) ||
            !TileRenderer.Check(settings, logger))
        {
            error = true;
        }

        cancellationToken.ThrowIfCancellationRequested();

        foreach (var dir in expectedStaticDirectories)
        {
            var fullDir = Path.GetFullPath(Path.Combine(Program.StaticDataDir, dir));

            if (!Directory.Exists(fullDir))
            {
                Directory.CreateDirectory(fullDir);
                logger.Warning($"Static data directory '{fullDir}' did not exist, created");
            }
        }

        foreach (var file in expectedStaticFiles)
        {
            var fullFile = Path.GetFullPath(Path.Combine(Program.StaticDataDir, file));

            if (!File.Exists(fullFile))
            {
                logger.Error($"Static data file '{fullFile}' does not exist");
                error = true;
            }
        }

        foreach (var (template, minimumVersion) in expectedVersionedStaticFiles)
        {
            var fileName = Path.GetFileName(template);
            var directory = Path.GetFullPath(Path.Combine(Program.StaticDataDir, Path.GetDirectoryName(template)!));

            if (!File.TryFindCompatibleFile(directory, minimumVersion, fileName, out var path))
            {
                logger.Error("Static data file '{Path}' does not exist, is outdated, or unsupported. Minimum version is {MinimumVersion}", Path.GetFullPath(Path.Combine(Program.StaticDataDir, template)), minimumVersion);
                error = true;
            }
            else
            {
                logger.Debug("Versioned static file found '{Path}'", path);
            }
        }

        logger.Debug("All static files exist");

        var resourcePack = new FileInfo(Path.GetFullPath(Path.Combine(Program.StaticDataDir, "resourcepacks", "vanilla.zip")));
        if (!resourcePack.Exists)
        {
            logger.Error($"Resourcepack file '{resourcePack.FullName}' does not exist");
            logger.Information("Download it from https://cdn.mceserv.net/availableresourcepack/resourcepacks/dba38e59-091a-4826-b76a-a08d7de5a9e2-1301b0c257a311678123b9e7325d0d6c61db3c35 (using internet archive)");
            logger.Information($"Rename it to vanilla.zip and move it to: {Path.GetFullPath(Path.Combine(Program.StaticDataDir, "resourcepacks"))}");

            error = true;
        }
        else if (resourcePack.Length < 100_000_000)
        {
            logger.Error($"Resourcepack file '{resourcePack.FullName}' is invalid, expected size: 131885348B, actual size: {resourcePack.Length}B");
            logger.Information("Download it from https://cdn.mceserv.net/availableresourcepack/resourcepacks/dba38e59-091a-4826-b76a-a08d7de5a9e2-1301b0c257a311678123b9e7325d0d6c61db3c35 (using internet archive)");
            logger.Information($"Rename it to vanilla.zip and move it to: {Path.GetFullPath(Path.Combine(Program.StaticDataDir, "resourcepacks"))}");

            error = true;
        }

        if (checkImporter)
        {
            // todo:
            // if (!BuildplateImporter.Check(settings, logger))
            // {
            //     error = true;
            // }
        }
        else
        {
            if (!Directory.EnumerateFiles(Path.Combine(Program.StaticDataDir, "server_template_dir", "mods")).Any(path => Path.GetFileName(path).StartsWith("fabric-api", StringComparison.OrdinalIgnoreCase) && path.EndsWith(".jar", StringComparison.OrdinalIgnoreCase)))
            {
                logger.Warning("Fabric api mod not found, downloading");

                var response = await httpClient.GetAsync("https://cdn.modrinth.com/data/P7dR8mSH/versions/xklQBMta/fabric-api-0.97.0%2B1.20.4.jar", cancellationToken);
                using (var fs = File.OpenWriteNew(Path.Combine(Program.StaticDataDir, "server_template_dir", "mods", "fabric-api-0.97.0+1.20.4.jar")))
                {
                    await response.Content.CopyToAsync(fs, cancellationToken);
                }

                logger.Information("Downloaded fabric api");
            }

            if (!File.Exists(Path.Combine(Program.StaticDataDir, "server_template_dir", BuildplateLauncher.ServerJarName)))
            {
                logger.Warning("Fabric server not found, downloading");

                var response = await httpClient.GetAsync("https://meta.fabricmc.net/v2/versions/loader/1.20.4/0.15.10/1.0.1/server/jar", cancellationToken);
                using (var fs = File.OpenWriteNew(Path.Combine(Program.StaticDataDir, "server_template_dir", BuildplateLauncher.ServerJarName)))
                {
                    await response.Content.CopyToAsync(fs, cancellationToken);
                }

                logger.Information("Downloaded fabric server");
            }

            string eulaPath = Path.GetFullPath(Path.Combine(Program.StaticDataDir, "server_template_dir", "eula.txt"));
            if (!File.Exists(eulaPath))
            {
                logger.Information("Detected that server was not setup, running");

                string javaExe = JavaLocator.Locate(logger);

                bool useShellExecute = false;

                using var serverProcess = new ConsoleProcess(javaExe, useShellExecute, !useShellExecute);

                if (!useShellExecute)
                {
                    serverProcess.StandartTextReceived += (sender, e) =>
                    {
                        if (!string.IsNullOrWhiteSpace(e.Data))
                        {
                            logger.Debug($"[server] {e.Data}");
                        }
                    };
                    serverProcess.ErrorTextReceived += (sender, e) =>
                    {
                        if (!string.IsNullOrWhiteSpace(e.Data))
                        {
                            logger.Error($"[server] {e.Data}");
                        }
                    };
                }

                await serverProcess.ExecuteAsync(Path.GetFullPath(Path.Combine(Program.StaticDataDir, "server_template_dir")), ["-jar", BuildplateLauncher.ServerJarName, "-nogui"]);
                logger.Information("Server process started, waiting for exit");
                await serverProcess.Process.WaitForExitAsync(cancellationToken);

                int exitCode = serverProcess.Process.ExitCode;
                logger.Information($"Server process exited with exit code {exitCode}");
                if (exitCode != 0)
                {
                    error = true;
                }
            }

            if (File.Exists(eulaPath) && !(await File.ReadAllTextAsync(eulaPath, cancellationToken)).Contains("eula=true", StringComparison.OrdinalIgnoreCase))
            {
                logger.Information($"Server eula not accepted, open '{eulaPath}' and set 'eula=true'");
                logger.Information("Waiting for you to make the change");
                while (!(await File.ReadAllTextAsync(eulaPath, cancellationToken)).Contains("eula=true", StringComparison.OrdinalIgnoreCase))
                {
                    await Task.Delay(1000, cancellationToken);
                }

                logger.Information("Running server to download/generate rest of the files, close it after it starts up");

                string javaExe = JavaLocator.Locate(logger);

                bool useShellExecute = true;

                using var serverProcess = new ConsoleProcess(javaExe, useShellExecute, !useShellExecute);

                if (!useShellExecute)
                {
                    serverProcess.StandartTextReceived += (sender, e) =>
                    {
                        if (!string.IsNullOrWhiteSpace(e.Data))
                        {
                            logger.Debug($"[server] {e.Data}");
                        }
                    };
                    serverProcess.ErrorTextReceived += (sender, e) =>
                    {
                        if (!string.IsNullOrWhiteSpace(e.Data))
                        {
                            logger.Error($"[server] {e.Data}");
                        }
                    };
                }

                await serverProcess.ExecuteAsync(Path.GetFullPath(Path.Combine(Program.StaticDataDir, "server_template_dir")), ["-jar", BuildplateLauncher.ServerJarName, "-nogui"]);
                logger.Information("Server process started, waiting for exit");
                await serverProcess.Process.WaitForExitAsync(cancellationToken);

                int exitCode = serverProcess.Process.ExitCode;
                logger.Information($"Server process exited with exit code {exitCode}");
                if (exitCode != 0)
                {
                    error = true;
                }
            }
        }

        return !error;
    }
}
