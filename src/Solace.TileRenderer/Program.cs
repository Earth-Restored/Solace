using Npgsql;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using Solace.EventBus.Client;
using Solace.StaticData;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Hosting;
using Solace.Common;
using Microsoft.Extensions.DependencyInjection;
using System.Runtime.Loader;

namespace Solace.TileRenderer;

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
                    var logger = GlobalLoggerFactory.CreateLogger(nameof(App));
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
        builder.Services.AddSingleton(sp => sp.GetRequiredService<StartupDependencies>().TileDataSource);
        builder.Services.AddSingleton<EventBusTileRenderer>();

        using var app = builder.Build();

        var loggerFactory = app.Services.GetRequiredService<ILoggerFactory>();
        GlobalLoggerFactory.Initialize(loggerFactory);

        var programLogger = loggerFactory.CreateLogger(nameof(App));

        ITileDataSource tileDataSource;
        if (!string.IsNullOrWhiteSpace(builder.Configuration["TileSource:MapTilerApiKey"]))
        {
            LogVerifyingMaptilerApiKey(programLogger);

            var maptilerApiKey = builder.Configuration["TileSource:MapTilerApiKey"];
            Debug.Assert(maptilerApiKey is not null);

            var httpClient = new HttpClient();
            HttpResponseMessage response;
            try
            {
                response = await httpClient.GetAsync($"https://api.maptiler.com/tiles/v3/tiles.json?key={maptilerApiKey}");
            }
            catch (HttpRequestException exception)
            {
                LogCouldNotConnectToMaptilerApi(programLogger, exception);
                loggerFactory.Dispose();
                return 3;
            }

            if (!response.IsSuccessStatusCode)
            {
                LogMaptilerApiKeyNotValid(programLogger, response.StatusCode);
                loggerFactory.Dispose();
                return 4;
            }

            var json = await JsonSerializer.DeserializeAsync<JsonObject>(response.Content.ReadAsStream());

            int maxZoom;
            if (json is null || !json.TryGetPropertyValue("maxzoom", out JsonNode? maxZoomNode) || maxZoomNode is not JsonValue maxZoomValue || maxZoomValue.GetValueKind() != JsonValueKind.Number)
            {
                maxZoom = 15;
                LogInvalidMaptilerResponse(programLogger, maxZoom);
            }
            else
            {
                maxZoom = maxZoomValue.GetValue<int>();
            }

            tileDataSource = new MaptilerTileDataSource(maptilerApiKey, maxZoom, httpClient);

            LogVerifiedMaptilerApiKey(programLogger);
        }
        else if (!string.IsNullOrWhiteSpace(builder.Configuration["TileSource:TileDatabaseConnectionString"]))
        {
            LogConnectingToTileDatabase(programLogger);

            var tileDatabaseConnectionString = builder.Configuration["TileSource:TileDatabaseConnectionString"];

            Debug.Assert(tileDatabaseConnectionString is not null);

            try
            {
                tileDataSource = new DatabaseTileDataSource(NpgsqlDataSource.Create(tileDatabaseConnectionString));
            }
            catch (Exception exception)
            {
                LogCouldNotConnectToTileDatabase(programLogger, exception);

                if (exception is ArgumentException)
                {
                    LogTileDatabaseConnectionStringFormatInvalid(programLogger, tileDatabaseConnectionString);
                }

                loggerFactory.Dispose();
                return 5;
            }

            LogConnectedToTileDatabase(programLogger);
        }
        else
        {
            LogNoTileDataSourceProvided(programLogger);
            loggerFactory.Dispose();
            return 6;
        }

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
        startupDeps.TileDataSource = tileDataSource;
        startupDeps.StaticData = staticData;
        startupDeps.EventBus = eventBusClient;

        try
        {
            var renderer = app.Services.GetRequiredService<EventBusTileRenderer>();
            await renderer.RunAsync();
        }
        catch (IOException exception)
        {
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
        public ITileDataSource TileDataSource { get; set; } = null!;
    }

    [LoggerMessage(Level = LogLevel.Critical, Message = "Unhandled exception")]
    private static partial void LogUnhandledException(ILogger logger, Exception? exception);

    [LoggerMessage(Level = LogLevel.Information, Message = "Verifying maptiler api key")]
    private static partial void LogVerifyingMaptilerApiKey(ILogger logger);

    [LoggerMessage(Level = LogLevel.Critical, Message = "Could not connect to maptiler api")]
    private static partial void LogCouldNotConnectToMaptilerApi(ILogger logger, Exception exception);

    [LoggerMessage(Level = LogLevel.Critical, Message = "Maptiler api key not valid, response status code: {StatusCode}")]
    private static partial void LogMaptilerApiKeyNotValid(ILogger logger, System.Net.HttpStatusCode StatusCode);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Invalid maptiler response, using default max zoom: {MaxZoom}")]
    private static partial void LogInvalidMaptilerResponse(ILogger logger, int MaxZoom);

    [LoggerMessage(Level = LogLevel.Information, Message = "Verified maptiler api key")]
    private static partial void LogVerifiedMaptilerApiKey(ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Connecting to tile database")]
    private static partial void LogConnectingToTileDatabase(ILogger logger);

    [LoggerMessage(Level = LogLevel.Critical, Message = "Could not connect to tile database")]
    private static partial void LogCouldNotConnectToTileDatabase(ILogger logger, Exception exception);

    [LoggerMessage(Level = LogLevel.Information, Message = "The provided connection string is: '{TileDatabaseConnectionString}', make sure that it is in the correct format")]
    private static partial void LogTileDatabaseConnectionStringFormatInvalid(ILogger logger, string TileDatabaseConnectionString);

    [LoggerMessage(Level = LogLevel.Information, Message = "Connected to tile database")]
    private static partial void LogConnectedToTileDatabase(ILogger logger);

    [LoggerMessage(Level = LogLevel.Critical, Message = "No tile data source provided")]
    private static partial void LogNoTileDataSourceProvided(ILogger logger);

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