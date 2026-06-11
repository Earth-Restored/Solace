using Serilog;
using Solace.Common.Utils;
using System.Diagnostics;
using System.Runtime.InteropServices;
using ILogger = Serilog.ILogger;

namespace Solace.LauncherUI.Programs;

internal static class BuildplateLauncher
{
    public static readonly string ExeName = "BuildplateLauncher" + (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? ".exe" : "");
    public const string DispName = "Buildplate launcher";

    public const string ServerJarName = "fabric-server-mc.1.20.4-loader.0.15.10-launcher.1.0.1.jar";

    public static readonly Version MinimumFountainBridgeVersion = new Version(0, 0, 2);
    public static readonly Version MinimumBuildplateConnectorPluginVersion = new Version(0, 0, 1);

#pragma warning disable IDE0060 // Remove unused parameter
    public static bool Check(Settings settings, ILogger logger)
#pragma warning restore IDE0060 // Remove unused parameter
    {
        string exePath = Path.GetFullPath(Path.Combine(Program.ProgramsDir, ExeName));
        if (!File.Exists(exePath))
        {
            logger.Error($"{DispName} exe doesn't exits: {exePath}");
            return false;
        }

        return true;
    }

    public static Process? Run(Settings settings, ILogger logger)
    {
        logger.Debug($"Running {DispName}");

        var serverJarsDir = Path.GetFullPath(Path.Combine(Program.StaticDataDir, "server_jars"));

        if (!File.TryFindCompatibleFile(serverJarsDir, MinimumBuildplateConnectorPluginVersion, "buildplate-connector-plugin-{{version}}-SNAPSHOT-jar-with-dependencies.jar", out var connectorPluginPath))
        {
            logger.Error("Could not find buildplate connector plugin jar, expected '{Path}', with minimum version {Version}", Path.Combine(serverJarsDir, "buildplate-connector-plugin-{{version}}-SNAPSHOT-jar-with-dependencies.jar"), MinimumBuildplateConnectorPluginVersion);
            return null;
        }

        if (!File.TryFindCompatibleFile(serverJarsDir, MinimumFountainBridgeVersion, "fountain-{{version}}-SNAPSHOT-jar-with-dependencies.jar", out var fountainBridgePath))
        {
            logger.Error("Could not find fountain bridge jar, expected '{Path}', with minimum version {Version}", Path.Combine(serverJarsDir, "fountain-{{version}}-SNAPSHOT-jar-with-dependencies.jar"), MinimumFountainBridgeVersion);
            return null;
        }
        
        return Process.Start(new ProcessStartInfo(Path.GetFullPath(Path.Combine(Program.ProgramsDir, ExeName)),
        [
            $"--eventbus=localhost:{settings.EventBusPort}",
            $"--publicAddress={settings.IPv4}",
            $"--bridgeJar={fountainBridgePath}",
            $"--serverTemplateDir={Path.GetFullPath(Path.Combine(Program.StaticDataDir, "server_template_dir"))}",
            $"--fabricJarName={ServerJarName}",
            $"--connectorPluginJar={connectorPluginPath}",
            $"--dir={Program.StaticDataDir}",
            $"--logger-url={Program.LoggerAddress}",
        ])
        {
            WorkingDirectory = Path.GetFullPath(Program.ProgramsDir),
            CreateNoWindow = false,
            UseShellExecute = true,
        });
    }
}
