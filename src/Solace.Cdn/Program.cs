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
    private static string staticDataPath = null!;

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static async Task<int> Run(string[] args)
    {
        var builder = WebApplication.CreateSlimBuilder(args);

        var isEFDesignTime = EF.IsDesignTime;

        staticDataPath = builder.Configuration["StaticDataPath"]!;

        if (!isEFDesignTime && !File.Exists(Path.Combine(staticDataPath, "resourcepacks", "vanilla.zip")))
        {
            Console.Error.WriteLine("Resource pack file does not exist");
            return 1;
        }

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

        builder.AddServiceDefaults();
        builder.WebHost.UseKestrelHttpsConfiguration();

        using var app = builder.Build();

        var loggerFactory = app.Services.GetRequiredService<ILoggerFactory>();
        GlobalLoggerFactory.Initialize(loggerFactory);

        var programLogger = loggerFactory.CreateLogger(nameof(Program));

        var forwardedHeadersOptions = new ForwardedHeadersOptions
        {
            ForwardedHeaders = ForwardedHeaders.All,
        };

        forwardedHeadersOptions.KnownIPNetworks.Clear();
        forwardedHeadersOptions.KnownProxies.Clear();

        app.UseForwardedHeaders(forwardedHeadersOptions);

        // app.UseHttpsRedirection();

        app.MapMethods("/availableresourcepack/resourcepacks/dba38e59-091a-4826-b76a-a08d7de5a9e2-1301b0c257a311678123b9e7325d0d6c61db3c35", ["GET", "HEAD"], GetResourcePackHandler);

        app.MapGet("/tile/{_0}/{_1}/{tilePos1}_{tilePos2}_{zoom}.png", HandleGetTile)
        .CacheOutput(policy => policy.Expire(TimeSpan.FromHours(1)));

        app.MapDefaultEndpoints();

        var startupDeps = app.Services.GetRequiredService<StartupDependencies>();

        var eventBusConnectionString = builder.Configuration["services:event-bus:grpc:0"];
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

        var objectStoreConnectionString = builder.Configuration["services:object-store:grpc:0"];
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

        startupDeps.EventBus = eventBus;
        startupDeps.ObjectStore = objectStore;

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

    private static Results<BadRequest, PhysicalFileHttpResult> GetResourcePackHandler(HttpContext context, ILogger<App> logger)
    {
        var resourcePackFilePath = Path.Combine(staticDataPath, "resourcepacks", "vanilla.zip"); // resource packs are distributed as renamed zip files containing an MCpack

        if (!System.IO.File.Exists(resourcePackFilePath))
        {
            LogResourcepackNotFound(logger);
            return TypedResults.BadRequest(); // we cannot serve you.
        }

        return TypedResults.PhysicalFile(
            path: resourcePackFilePath,
            contentType: "application/octet-stream",
            fileDownloadName: "dba38e59-091a-4826-b76a-a08d7de5a9e2-1301b0c257a311678123b9e7325d0d6c61db3c35",
            enableRangeProcessing: true
        );
    }

    internal sealed class StartupDependencies
    {
        public EventBusClient EventBus { get; set; } = null!;
        public ObjectStoreClient ObjectStore { get; set; } = null!;
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
}
