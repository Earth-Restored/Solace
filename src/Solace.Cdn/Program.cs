using System.Diagnostics;
using System.Runtime.CompilerServices;

#if USE_SHARED_LIBS
using System.Runtime.Loader;
#endif
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Solace.Cdn.Utils;
using Solace.Common;
using Solace.Db.Earth;
using Solace.EventBus.Client;
using Solace.ObjectStore.Client;
using Solace.StaticData;

namespace Solace.Cdn;

internal static class Program
{
    private static async Task Main(string[] args)
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

        await App.Run(args);
    }
}

#pragma warning disable MA0048 // File name must match type name
internal sealed partial class App
#pragma warning restore MA0048 // File name must match type name
{
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static async Task<int> Run(string[] args)
    {
        var builder = WebApplication.CreateSlimBuilder(args);

        var isEFDesignTime = EF.IsDesignTime;

        var staticDataPath = builder.Configuration["StaticDataPath"]!;

        var earthDbConnectionString = builder.Configuration.GetConnectionString("EarthDb");
        if (isEFDesignTime)
        {
            earthDbConnectionString ??= "Host=localhost;Database=dummy;";
        }

        Debug.Assert(earthDbConnectionString is not null);

        builder.Services.AddDbContextFactory<EarthDbContext>(options =>
            EarthDbContext.ConfigureBuilder(options, earthDbConnectionString));

        builder.Services.AddSingleton<StartupDependencies>();
        builder.Services.AddSingleton(sp => sp.GetRequiredService<StartupDependencies>().EventBus);
        builder.Services.AddSingleton(sp => sp.GetRequiredService<StartupDependencies>().ObjectStore);
        builder.Services.AddSingleton(sp => sp.GetRequiredService<StartupDependencies>().StaticData);

        builder.AddServiceDefaults();
        builder.WebHost.UseKestrelHttpsConfiguration();

        using var app = builder.Build();

        var loggerFactory = app.Services.GetRequiredService<ILoggerFactory>();
        GlobalLoggerFactory.Initialize(loggerFactory);

        var programLogger = loggerFactory.CreateLogger(nameof(Program));

        var startupDeps = app.Services.GetRequiredService<StartupDependencies>();

        var eventBusConnectionString = builder.Configuration["services:event-bus:http:0"];
        Debug.Assert(eventBusConnectionString is not null);

        LogConnectingToEventBus(programLogger);
        EventBusClient eventBus;
        try
        {
            eventBus = await EventBusClient.ConnectAsync(eventBusConnectionString, programLogger);
        }
        catch (Exception exception)
        {
            LogConnectToEventBusError(programLogger, exception);
            loggerFactory.Dispose();
            return 3;
        }

        LogConnectedToEventBus(programLogger);

        var objectStoreConnectionString = builder.Configuration["services:object-store:http:0"];
        Debug.Assert(objectStoreConnectionString is not null);

        LogConnectingToObjectStore(programLogger);
        ObjectStoreClient objectStore;
        try
        {
            objectStore = await ObjectStoreClient.ConnectAsync(objectStoreConnectionString, programLogger);
        }
        catch (Exception exception)
        {
            LogConnectToObjectStoreError(programLogger, exception);
            loggerFactory.Dispose();
            return 4;
        }

        LogConnectedToObjectStore(programLogger);

        LogLoadingStaticData(programLogger);
        StaticDataProvider staticData;
        try
        {
            staticData = new StaticDataProvider(builder.Configuration["StaticDataPath"]!);
        }
        catch (StaticDataException exception)
        {
            LogLoadStaticDataError(programLogger, exception);
            loggerFactory.Dispose();
            return 5;
        }

        LogLoadedStaticData(programLogger);

        startupDeps.EventBus = eventBus;
        startupDeps.ObjectStore = objectStore;
        startupDeps.StaticData = staticData;

        if (!isEFDesignTime && staticData.Resourcepacks.GenoaResourcepackPath is null)
        {
            Console.Error.WriteLine("Resource pack file does not exist");
            return 1;
        }

        var forwardedHeadersOptions = new ForwardedHeadersOptions
        {
            ForwardedHeaders = ForwardedHeaders.All,
        };

        forwardedHeadersOptions.KnownIPNetworks.Clear();
        forwardedHeadersOptions.KnownProxies.Clear();

        app.UseForwardedHeaders(forwardedHeadersOptions);

        app.MapMethods($"/availableresourcepack/resourcepacks/{staticData.Resourcepacks.GenoaResourcepackName}", ["GET", "HEAD"], GetResourcePackHandler);

        app.MapGet("/tile/{_0}/{_1}/{tilePos1}_{tilePos2}_{zoom}.png", HandleGetTile)
        .CacheOutput(policy => policy.Expire(TimeSpan.FromHours(1)));

        app.MapDefaultEndpoints();

        app.Run();

        return 0;
    }

    private static async Task<Results<EmptyHttpResult, NotFound, BadRequest>> HandleGetTile([FromRoute] int _0, [FromRoute] int _1, [FromRoute] int tilePos1, [FromRoute] int tilePos2, [FromRoute] int zoom, HttpContext context, [FromServices] IDbContextFactory<EarthDbContext> earthDbFactory, [FromServices] EventBusClient eventBus, [FromServices] ObjectStoreClient objectStore, ILogger<App> logger, CancellationToken cancellationToken)
    {
        context.Response.Headers.CacheControl = "public,max-age=11200";
        var cd = new System.Net.Mime.ContentDisposition { FileName = $"{tilePos1}_{tilePos2}_{zoom}.png", Inline = true };
        context.Response.Headers.Append("Content-Disposition", cd.ToString());
        context.Response.Headers.ContentType = "application/octet-stream";

        if (zoom != 16)
        {
            return TypedResults.BadRequest();
        }

        if (!await TileUtils.TryWriteTile(tilePos1, tilePos2, zoom, context.Response.Body, earthDbFactory, eventBus, objectStore, logger, cancellationToken))
        {
            return TypedResults.NotFound();
        }

        return TypedResults.Empty;
    }

    private static Results<BadRequest, PhysicalFileHttpResult> GetResourcePackHandler(HttpContext context, StaticDataProvider staticData, ILogger<App> logger)
    {
        var resourcePackFilePath = staticData.Resourcepacks.GenoaResourcepackPath;

        if (!System.IO.File.Exists(resourcePackFilePath))
        {
            LogResourcepackNotFound(logger);
            return TypedResults.BadRequest();
        }

        return TypedResults.PhysicalFile(
            path: resourcePackFilePath,
            contentType: "application/octet-stream",
            fileDownloadName: staticData.Resourcepacks.GenoaResourcepackName,
            enableRangeProcessing: true
        );
    }

    internal sealed class StartupDependencies
    {
        public EventBusClient EventBus { get; set; } = null!;
        public ObjectStoreClient ObjectStore { get; set; } = null!;
        public StaticDataProvider StaticData { get; set; } = null!;
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Resource pack file not found")]
    public static partial void LogResourcepackNotFound(ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Connecting to event bus")]
    public static partial void LogConnectingToEventBus(ILogger logger);

    [LoggerMessage(Level = LogLevel.Critical, Message = "Could not connect to event bus")]
    public static partial void LogConnectToEventBusError(ILogger logger, Exception exception);

    [LoggerMessage(Level = LogLevel.Information, Message = "Connected to event bus")]
    public static partial void LogConnectedToEventBus(ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Connecting to object store")]
    public static partial void LogConnectingToObjectStore(ILogger logger);

    [LoggerMessage(Level = LogLevel.Critical, Message = "Could not connect to object store")]
    public static partial void LogConnectToObjectStoreError(ILogger logger, Exception exception);

    [LoggerMessage(Level = LogLevel.Information, Message = "Connected to object store")]
    public static partial void LogConnectedToObjectStore(ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Loading static data")]
    private static partial void LogLoadingStaticData(ILogger logger);

    [LoggerMessage(Level = LogLevel.Critical, Message = "Failed to load static data")]
    private static partial void LogLoadStaticDataError(ILogger logger, Exception exception);

    [LoggerMessage(Level = LogLevel.Information, Message = "Loaded static data")]
    private static partial void LogLoadedStaticData(ILogger logger);
}
