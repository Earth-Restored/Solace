using System.Diagnostics;
using Solace.EventBus.Client;
using Solace.StaticData;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Solace.Common;
using System.Runtime.Loader;

namespace Solace.TappablesGenerator;

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

internal static partial class App
{
    public static async Task<int> RunAsync(string[] args)
    {
        if (!Debugger.IsAttached)
        {
            AppDomain.CurrentDomain.UnhandledException += (object sender, UnhandledExceptionEventArgs e) =>
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

        var builder = Host.CreateApplicationBuilder(args);

        builder.AddServiceDefaults();

        builder.Services.AddSingleton<StartupDependencies>();
        builder.Services.AddSingleton(sp => sp.GetRequiredService<StartupDependencies>().EventBus);
        builder.Services.AddSingleton(sp => sp.GetRequiredService<StartupDependencies>().StaticData);
        builder.Services.AddSingleton<TappableGenerator>();
        builder.Services.AddSingleton<EncounterGenerator>();
        builder.Services.AddSingleton<ActiveTiles>();
        builder.Services.AddSingleton<Spawner>();

        using var app = builder.Build();

        var loggerFactory = app.Services.GetRequiredService<ILoggerFactory>();
        GlobalLoggerFactory.Initialize(loggerFactory);

        var programLogger = loggerFactory.CreateLogger(nameof(Program));

        LogLoadingStaticData(programLogger);
        StaticData.StaticData staticData;
        try
        {
            staticData = new StaticData.StaticData(builder.Configuration["StaticDataPath"]!);
        }
        catch (StaticDataException exception)
        {
            LogLoadStaticDataError(programLogger, exception);
            loggerFactory.Dispose();
            return 3;
        }

        LogLoadedStaticData(programLogger);

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

        // init stuff that requires logger but needs to be injected
        var startupDeps = app.Services.GetRequiredService<StartupDependencies>();
        startupDeps.StaticData = staticData;
        startupDeps.EventBus = eventBusClient;

        // init stuff that needs async initialization
        var spawner = app.Services.GetRequiredService<Spawner>();
        await app.Services.GetRequiredService<ActiveTiles>().InitializeAsync(eventBusClient, new ActiveTiles.ActiveTileListener(
            async activeTiles =>
            {
                await spawner.SpawnTiles(activeTiles);
            },
            async activeTile =>
            {
                // empty
            }
        ));
        await spawner.InitializeAsync(eventBusClient);

        try
        {
            await spawner.RunAsync();
        }
        catch (IOException exception)
        {
            Console.Error.WriteLine(exception.ToString());
            LogFatalErrorDuringServerStartup(programLogger, exception);
            loggerFactory.Dispose();
            return 1;
        }

        return 0;
    }

    internal sealed class StartupDependencies
    {
        public EventBusClient EventBus { get; set; } = null!;
        public StaticData.StaticData StaticData { get; set; } = null!;
    }

    [LoggerMessage(Level = LogLevel.Critical, Message = "Unhandled exception")]
    private static partial void LogUnhandledException(ILogger logger, Exception? exception);

    [LoggerMessage(Level = LogLevel.Information, Message = "Loading static data")]
    private static partial void LogLoadingStaticData(ILogger logger);

    [LoggerMessage(Level = LogLevel.Critical, Message = "Failed to load static data")]
    private static partial void LogLoadStaticDataError(ILogger logger, Exception exception);

    [LoggerMessage(Level = LogLevel.Information, Message = "Loaded static data")]
    private static partial void LogLoadedStaticData(ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Connecting to event bus")]
    private static partial void LogConnectingToEventBus(ILogger logger);

    [LoggerMessage(Level = LogLevel.Critical, Message = "Could not connect to event bus")]
    private static partial void LogConnectToEventBusError(ILogger logger, Exception exception);

    [LoggerMessage(Level = LogLevel.Information, Message = "Connected to event bus")]
    private static partial void LogConnectedToEventBus(ILogger logger);

    [LoggerMessage(Level = LogLevel.Critical, Message = "Fatal error during server startup")]
    private static partial void LogFatalErrorDuringServerStartup(ILogger logger, Exception exception);
}
