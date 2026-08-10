using System.Diagnostics;
using Solace.Common;
using Solace.Common.Utils;
using Solace.EventBus.Client;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
#if USE_SHARED_LIBS
using System.Runtime.Loader;
#endif

namespace Solace.Buildplate.Launcher;

internal static class Program
{
    private static Task<int> Main(string[] args)
    {
#if USE_SHARED_LIBS
        AssemblyLoadContext.Default.Resolving += (context, assemblyName) =>
        {
            string sharedDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "shared_libs"));
            string assemblyPath = Path.Combine(sharedDir, $"{assemblyName.Name}.dll");

            if (File.Exists(assemblyPath))
            {
                return context.LoadFromAssemblyPath(assemblyPath);
            }

            return null;
        };
#endif

        return App.RunAsync(args);
    }
}

#pragma warning disable MA0048 // File name must match type name
internal static partial class App
#pragma warning restore MA0048 // File name must match type name
{
    internal static string StaticDataPath = "./staticdata";

    public static readonly Version MinimumFountainBridgeVersion = new(0, 0, 2);
    public static readonly Version MinimumBuildplateConnectorPluginVersion = new(0, 1, 0);

    public static async Task<int> RunAsync(string[] args)
    {
        if (!Debugger.IsAttached)
        {
            AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
            {
                Console.Error.WriteLine($"Unhandled exception: {e.ExceptionObject}");

                try
                {
                    var logger = GlobalLoggerFactory.CreateLogger(nameof(Program));
                    LogUnhandledException(logger, e.ExceptionObject as Exception);
                }
                catch
                {
                    Console.Error.WriteLine($"Unhandled exception before logger initialization");
                }

                Console.Out.Flush();
                Console.Error.Flush();

                Environment.Exit(2);
            };
        }

        // ProcessStartInfo.KillOnParentExit currently only supported on these
        if (!OperatingSystem.IsWindows() && !OperatingSystem.IsLinux() && !OperatingSystem.IsAndroid())
        {
            Console.WriteLine("Unsupported OS, supported: windows, linux, android");
            return 8;
        }

        var builder = Host.CreateApplicationBuilder(args);

        if (!builder.Configuration.GetValue<bool>("AcceptMinecraftEula"))
        {
            Console.Write("Error: you must accept the minecraft eula, change AcceptMinecraftEula to true");
            return 3;
        }

        builder.AddServiceDefaults();

        builder.Services.AddSingleton<StartupDependencies>();
        builder.Services.AddSingleton(sp => sp.GetRequiredService<StartupDependencies>().EventBus);
        builder.Services.AddSingleton(sp => sp.GetRequiredService<StartupDependencies>().Starter);
        builder.Services.AddSingleton<InstanceManager>();

        using var app = builder.Build();

        var loggerFactory = app.Services.GetRequiredService<ILoggerFactory>();
        GlobalLoggerFactory.Initialize(loggerFactory);

        var programLogger = loggerFactory.CreateLogger(nameof(Program));

        {
            var staticDataPath = builder.Configuration["StaticDataPath"];
            Debug.Assert(staticDataPath is not null);
            StaticDataPath = Path.GetFullPath(staticDataPath);
        }

        // init stuff that requires logger but needs to be injected
        var startupDeps = app.Services.GetRequiredService<StartupDependencies>();

        var eventBusConnectionString = builder.Configuration["services:event-bus:http:0"];
        Debug.Assert(eventBusConnectionString is not null);

        LogConnectingToEventBus(programLogger);
        EventBusClient eventBusClient;
        try
        {
            eventBusClient = await EventBusClient.ConnectAsync(eventBusConnectionString, programLogger);
        }
        catch (Exception exception)
        {
            LogConnectToEventBusError(programLogger, exception);
            loggerFactory.Dispose();
            return 4;
        }

        LogConnectedToEventBus(programLogger);

        var javaCmd = JavaLocator.Locate(GlobalLoggerFactory.CreateLogger(nameof(JavaLocator)));

        var publicEndPoint = builder.Configuration["PublicEndPoint"];
        Debug.Assert(publicEndPoint is not null);

        var baseInstancePublicPort = checked((ushort)builder.Configuration.GetValue<int>("BaseInstancePublicPort"));

        var fabricJarName = builder.Configuration["FabricJarName"]!;

        var serverJarsDir = Path.Combine(StaticDataPath, "server_jars");

        var fountainBridgeJarName = builder.Configuration["FountainBridgeJarName"];
        Debug.Assert(fountainBridgeJarName is not null);

        if (!Path.IsPathFullyQualified(fountainBridgeJarName))
        {
            fountainBridgeJarName = Path.GetFullPath(Path.Combine(serverJarsDir, fountainBridgeJarName));
        }

        if ((fountainBridgeJarName = ResolveVersionedFile(fountainBridgeJarName, MinimumFountainBridgeVersion, programLogger)) is null)
        {
            return 6;
        }

        var connectorPluginJarName = builder.Configuration["ConnectorPluginJarName"];
        Debug.Assert(connectorPluginJarName is not null);

        if (!Path.IsPathFullyQualified(connectorPluginJarName))
        {
            connectorPluginJarName = Path.GetFullPath(Path.Combine(serverJarsDir, connectorPluginJarName));
        }

        if ((connectorPluginJarName = ResolveVersionedFile(connectorPluginJarName, MinimumBuildplateConnectorPluginVersion, programLogger)) is null)
        {
            return 7;
        }

        var starter = new Starter(eventBusClient, eventBusConnectionString, publicEndPoint, baseInstancePublicPort, javaCmd, fountainBridgeJarName, Path.GetFullPath(Path.Combine(StaticDataPath, "server_template_dir")), fabricJarName, connectorPluginJarName, loggerFactory, GlobalLoggerFactory.CreateLogger<Starter>());

        startupDeps.EventBus = eventBusClient;
        startupDeps.Starter = starter;

        // init stuff that needs async initialization
        var instanceManager = app.Services.GetRequiredService<InstanceManager>();
        await instanceManager.InitializeAsync(eventBusClient);

        Console.CancelKeyPress += (sender, e) =>
        {
            LogCtrlCReceived(programLogger);
            instanceManager.ShutdownAsync().Forget();
            e.Cancel = true;
        };

        AppDomain.CurrentDomain.ProcessExit += (sender, e) =>
        {
            instanceManager.ShutdownAsync().Wait();
        };

        LogStarted(programLogger, publicEndPoint, baseInstancePublicPort);

        while (true)
        {
            await Task.Delay(1000);
        }
    }

    public static string? ResolveVersionedFile(string? path, Version minimumVersion, ILogger logger)
    {
        if (path is null)
        {
            LogStaticDataNotSpecified(logger);
            return null;
        }

        if (path.Contains("{{version}}", StringComparison.Ordinal))
        {
            var fileName = Path.GetFileName(path);
            var directory = Path.GetDirectoryName(path)!;

            if (!File.TryFindCompatibleFile(directory, minimumVersion, fileName, out var matchingFile))
            {
                LogVersionedStaticDataNotFoundError(logger, Path.GetFullPath(Path.Combine(StaticDataPath, path)), minimumVersion);
                return null;
            }
            else
            {
                path = matchingFile;
                LogVersionedStaticFileFound(logger, path);
            }
        }

        return path;
    }

    internal sealed class StartupDependencies
    {
        public EventBusClient EventBus { get; set; } = null!;
        public Starter Starter { get; set; } = null!;
    }

    [LoggerMessage(Level = LogLevel.Critical, Message = "Unhandled exception")]
    private static partial void LogUnhandledException(ILogger logger, Exception? exception);

    [LoggerMessage(Level = LogLevel.Information, Message = "Connecting to event bus")]
    private static partial void LogConnectingToEventBus(ILogger logger);

    [LoggerMessage(Level = LogLevel.Critical, Message = "Could not connect to event bus")]
    private static partial void LogConnectToEventBusError(ILogger logger, Exception exception);

    [LoggerMessage(Level = LogLevel.Information, Message = "Connected to event bus")]
    private static partial void LogConnectedToEventBus(ILogger logger);

    [LoggerMessage(Level = LogLevel.Error, Message = "Parameter for a static data file was not supplied")]
    private static partial void LogStaticDataNotSpecified(ILogger logger);

    [LoggerMessage(Level = LogLevel.Error, Message = "Static data file '{Path}' does not exist, is outdated, or unsupported. Minimum version is {MinimumVersion}")]
    private static partial void LogVersionedStaticDataNotFoundError(ILogger logger, string Path, Version MinimumVersion);

    [LoggerMessage(Level = LogLevel.Information, Message = "Versioned static file found '{Path}'")]
    private static partial void LogVersionedStaticFileFound(ILogger logger, string Path);

    [LoggerMessage(Level = LogLevel.Information, Message = "Ctrl+C received")]
    private static partial void LogCtrlCReceived(ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Started, public address: {Address}, base port: {BasePort}")]
    private static partial void LogStarted(ILogger logger, string Address, int BasePort);
}
