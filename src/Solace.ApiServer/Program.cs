using Microsoft.EntityFrameworkCore;
using System.Diagnostics;
using Solace.ApiServer.Utils;
using Solace.BuildplateImporter;
using Solace.Common;
using Solace.DB;
using Solace.EventBus.Client;
using Solace.ObjectStore.Client;
using Solace.StaticData;
using SData = Solace.StaticData.StaticData;
using Microsoft.AspNetCore.Authentication;
using Asp.Versioning;
using Solace.ApiServer.Authentication;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.AspNetCore.HttpOverrides;
using System.Reflection;
#if USE_SHARED_LIBS
using System.Runtime.Loader;
#endif

namespace Solace.ApiServer;

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
        // Environment.CurrentDirectory = AppDomain.CurrentDomain.BaseDirectory;

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

        var builder = WebApplication.CreateBuilder(args);

        builder.AddServiceDefaults();

        var earthDbConnectionString = builder.Configuration.GetConnectionString("EarthDb");

        bool isEFTooling = Assembly.GetEntryAssembly()?.GetName().Name == "ef";

        if (isEFTooling)
        {
            earthDbConnectionString ??= "Host=localhost;Database=dummy;";
        }

        Debug.Assert(earthDbConnectionString is not null);

        builder.Services.AddSingleton<StartupDependencies>();
        builder.Services.AddSingleton(sp => sp.GetRequiredService<StartupDependencies>().EventBus);
        builder.Services.AddSingleton(sp => sp.GetRequiredService<StartupDependencies>().ObjectStore);
        builder.Services.AddSingleton(sp => sp.GetRequiredService<StartupDependencies>().StaticData);
        builder.Services.AddSingleton(sp => sp.GetRequiredService<StartupDependencies>().Secrets);
        builder.Services.AddSingleton<TappablesManager>();
        builder.Services.AddSingleton<BuildplateInstancesManager>();
        builder.Services.AddSingleton<BuildplateInstanceRequestHandler>();

        builder.Services.AddMemoryCache();

        builder.Services.AddSingleton<CatalogResponseCacheService>();

        builder.Services.AddControllers()
           .ConfigureApplicationPartManager(manager =>
           {
               manager.FeatureProviders.Add(new InternalControllerFeatureProvider());
           });

        builder.Services.AddResponseCompression(options =>
        {
            options.Providers.Add<GzipCompressionProvider>();
        });

        builder.Services.AddResponseCaching();

        builder.Services.AddApiVersioning(config =>
        {
            config.DefaultApiVersion = new ApiVersion(1, 1);
            config.AssumeDefaultVersionWhenUnspecified = true;
            config.ReportApiVersions = true;
        });

        builder.Services.AddAuthentication("GenoaAuth")
            .AddScheme<AuthenticationSchemeOptions, GenoaAuthenticationHandler>("GenoaAuth", null);

        builder.Services.AddDbContextFactory<EarthDbContext>(options =>
            EarthDbContext.ConfigureBuilder(options, earthDbConnectionString));

        await using var app = builder.Build();

        var loggerFactory = app.Services.GetRequiredService<ILoggerFactory>();
        GlobalLoggerFactory.Initialize(loggerFactory);

        var programLogger = loggerFactory.CreateLogger(nameof(Program));

        if (builder.Configuration.GetValue<bool>("Authentication:LocalLoginOnly"))
        {
            LogLocalAccountOnlyEnabled(programLogger);
        }
        else
        {
            LogLocalAccountOnlyDisabled(programLogger);
        }

        var forwardedHeadersOptions = new ForwardedHeadersOptions
        {
            ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto,
        };

        forwardedHeadersOptions.KnownIPNetworks.Clear();
        forwardedHeadersOptions.KnownProxies.Clear();

        app.UseForwardedHeaders(forwardedHeadersOptions);

        app.Use(async (context, next) =>
        {
            context.Items.Add(RequestExtensions.TimestampKey, DateTimeOffset.UtcNow);
            await next();
        });

        app.UseStaticFiles();

        app.UseRouting();

        app.UseAuthentication();
        app.UseAuthorization();

        app.UseETagger();

        app.UseResponseCaching();
        app.UseResponseCompression();

        app.MapControllers();

        // init stuff that requires logger but needs to be injected
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
        SData staticData;
        try
        {
            staticData = new SData(builder.Configuration["StaticDataPath"]!);
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

        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<EarthDbContext>();

            await db.Database.MigrateAsync();

            startupDeps.Secrets = await db.GetOrInitializeSecretsAsync();

            await ImportShopBuildplates(db, eventBus, objectStore, staticData, programLogger);
        }

        // init stuff that needs async initialization
        await app.Services.GetRequiredService<TappablesManager>().InitializeAsync(eventBus);
        await app.Services.GetRequiredService<BuildplateInstancesManager>().InitializeAsync(eventBus);
        await app.Services.GetRequiredService<BuildplateInstanceRequestHandler>().InitializeAsync(eventBus);

        await app.RunAsync();

        return 0;
    }

    private static async Task ImportShopBuildplates(EarthDbContext earthDbContext, EventBusClient eventBus, ObjectStoreClient objectStore, SData staticData, ILogger logger)
    {
        LogImportingShopBuildplates(logger);

        var currentShopBuildplates = await earthDbContext.TemplateBuildplates
            .AsNoTracking()
            .ToListAsync();

        await using var importer = new Importer(earthDbContext, eventBus, objectStore, logger)
        {
            OwnsEarthDb = false,
            OwnsEventBusClient = false,
            OwnsObjectStoreClient = false,
        };

        foreach (var buildplate in staticData.Buildplates.ShopBuildplates)
        {
            if (earthDbContext.TemplateBuildplates.Any(bp => bp.Id == buildplate.Id))
            {
                LogShopBuildplateAlreadyExists(logger, buildplate.Id);
                continue;
            }

            try
            {
                LogImportingShopBuildplate(logger, buildplate.Id);

                string name = "unknown buildplate";
                var bpPlayfabItem = staticData.Playfab.Items.Values.FirstOrDefault(item => item.Data is Playfab.Item.BuildplateData bpData && bpData.Id == buildplate.Id);
                if (bpPlayfabItem is not null)
                {
                    name = bpPlayfabItem.Title;
                }

                using (var buidplateData = buildplate.OpenRead())
                {
                    await importer.ImportTemplateAsync(buildplate.Id, $"[SHOP] {name}", buidplateData);
                }
            }
            catch (Exception exception)
            {
                LogFailedToImportShopBuidplate(logger, exception, buildplate.Id);
            }
        }

        LogImportedShopBuildplates(logger);
    }

    internal sealed class StartupDependencies
    {
        public EventBusClient EventBus { get; set; } = null!;
        public ObjectStoreClient ObjectStore { get; set; } = null!;
        public SData StaticData { get; set; } = null!;
        public CryptoSecrets Secrets { get; set; } = null!;
    }

    [LoggerMessage(Level = LogLevel.Critical, Message = "Unhandled exception")]
    private static partial void LogUnhandledException(ILogger logger, Exception? exception);

    [LoggerMessage(Level = LogLevel.Information, Message = "Local account only login enabled, Microsoft accounts will not work")]
    private static partial void LogLocalAccountOnlyEnabled(ILogger logger);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Local account only login disabled, account credentials cannot be verified")]
    private static partial void LogLocalAccountOnlyDisabled(ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Connecting to event bus")]
    private static partial void LogConnectingToEventBus(ILogger logger);

    [LoggerMessage(Level = LogLevel.Critical, Message = "Could not connect to event bus")]
    private static partial void LogConnectToEventBusError(ILogger logger, Exception exception);

    [LoggerMessage(Level = LogLevel.Information, Message = "Connected to event bus")]
    private static partial void LogConnectedToEventBus(ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Connecting to object store")]
    private static partial void LogConnectingToObjectStore(ILogger logger);

    [LoggerMessage(Level = LogLevel.Critical, Message = "Could not connect to object store")]
    private static partial void LogConnectToObjectStoreError(ILogger logger, Exception exception);

    [LoggerMessage(Level = LogLevel.Information, Message = "Connected to object store")]
    private static partial void LogConnectedToObjectStore(ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Loading static data")]
    private static partial void LogLoadingStaticData(ILogger logger);

    [LoggerMessage(Level = LogLevel.Critical, Message = "Failed to load static data")]
    private static partial void LogLoadStaticDataError(ILogger logger, Exception exception);

    [LoggerMessage(Level = LogLevel.Information, Message = "Loaded static data")]
    private static partial void LogLoadedStaticData(ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Importing shop buildplates")]
    private static partial void LogImportingShopBuildplates(ILogger logger);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Shop buildplate {BuildplateId} already exists")]
    private static partial void LogShopBuildplateAlreadyExists(ILogger logger, Guid BuildplateId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Importing shop buildplate {BuildplateId}")]
    private static partial void LogImportingShopBuildplate(ILogger logger, Guid BuildplateId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to import shop buidplate {BuildplateId}")]
    private static partial void LogFailedToImportShopBuidplate(ILogger logger, Exception exception, Guid BuildplateId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Imported shop buildplates")]
    private static partial void LogImportedShopBuildplates(ILogger logger);
}

