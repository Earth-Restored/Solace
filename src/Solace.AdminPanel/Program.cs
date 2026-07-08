using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Solace.Common;
using Solace.Common.Utils;
using Solace.DB;
using Solace.AdminPanel.Components;
using Solace.AdminPanel.Components.Account;
using Solace.AdminPanel.Data;
using Solace.AdminPanel.Utils;
using Solace.ObjectStore.Client;
using System.Diagnostics;
using System.Reflection;
using Solace.EventBus.Client;
using System.Runtime.Loader;

namespace Solace.AdminPanel;

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
    public static string Address { get; private set; } = "";

    public static async Task<int> RunAsync(string[] args)
    {
        // Environment.CurrentDirectory = AppContext.BaseDirectory;

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

        var builder = WebApplication.CreateBuilder(args);

        builder.AddServiceDefaults();

        var earthDbConnectionString = builder.Configuration.GetConnectionString("EarthDb");
        var earthDbProvider = builder.Configuration["DatabaseProvider"];

        bool isEFTooling = Assembly.GetEntryAssembly()?.GetName().Name == "ef";

        if (isEFTooling)
        {
            earthDbProvider ??= "Sqlite";
            earthDbConnectionString ??= "Data Source=dummy.db";
        }

        Debug.Assert(earthDbConnectionString is not null);
        Debug.Assert(earthDbProvider is not null);

        builder.Services.AddSingleton<StartupDependencies>();
        builder.Services.AddSingleton(sp => sp.GetRequiredService<StartupDependencies>().EventBus);
        builder.Services.AddSingleton(sp => sp.GetRequiredService<StartupDependencies>().ObjectStore);
        builder.Services.AddSingleton(sp => sp.GetRequiredService<StartupDependencies>().StaticData);

        // bool isLegacyDb = await IsLegacyEarthDbAsync(Settings.Instance.EarthDatabaseConnectionString!);
        // string legacyDbPath = "";
        // string liveDbPath = "";
        // if (isLegacyDb)
        // {
        //     Log.Information("Detected legacy db format, backing up db");
        //     legacyDbPath = Path.GetUniqueFilePath(Path.GetFullPath(Path.Combine(DataDirRelative, "earth.db.old")));
        //     File.Move(Settings.Instance.EarthDatabaseConnectionString!, legacyDbPath);
        //     Log.Debug($"Moved legacy earth db to '{legacyDbPath}'");

        //     liveDbPath = Path.GetUniqueFilePath(Path.GetFullPath(Path.Combine(DataDirRelative, "live.db.old")));
        //     File.Move(Settings.Instance.LiveDatabaseConnectionString!, liveDbPath);
        //     Log.Debug($"Moved legacy live db to '{liveDbPath}'");
        // }

        // bool isLegacyDb = true;
        // string legacyDbPath = Path.GetFullPath(Path.Combine(DataDirRelative, "earth.db.old"));
        // if (EF.IsDesignTime)
        // {
        //     isLegacyDb = false;
        // }
        // else
        // {
        //     Log.Information("Detected legacy db format, backing up db");
        //     if (File.Exists(legacyDbPath))
        //     {
        //         File.Delete(Settings.Instance.EarthDatabaseConnectionString!);
        //         File.Delete(Settings.Instance.EarthDatabaseConnectionString! + "-shm");
        //         File.Delete(Settings.Instance.EarthDatabaseConnectionString! + "-wal");
        //         await File.Create(Settings.Instance.EarthDatabaseConnectionString!).DisposeAsync(); // create and close it

        //         try
        //         {
        //             var dbFile = new FileInfo(Settings.Instance.EarthDatabaseConnectionString!);
        //             if (dbFile.Exists)
        //             {
        //                 dbFile.IsReadOnly = false;
        //                 File.SetAttributes(dbFile.FullName, FileAttributes.Normal);
        //             }
        //         }
        //         catch (Exception ex)
        //         {
        //             Log.Warning(ex, "Failed to normalize database file permissions");
        //         }
        //     }
        //     else
        //     {
        //         File.Move(Settings.Instance.EarthDatabaseConnectionString!, legacyDbPath);
        //         Log.Debug($"Moved legacy db to '{legacyDbPath}'");
        //     }
        // }

        // Add services to the container.
        builder.Services.AddRazorComponents()
            .AddInteractiveServerComponents();

        builder.Services.AddCascadingAuthenticationState();
        builder.Services.AddScoped<IdentityRedirectManager>();
        builder.Services.AddScoped<AuthenticationStateProvider, IdentityRevalidatingAuthenticationStateProvider>();

        builder.Services.AddAuthentication(options =>
            {
                options.DefaultScheme = IdentityConstants.ApplicationScheme;
                options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
            })
            .AddIdentityCookies();

        builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
        builder.Services.AddScoped<IAuthorizationHandler, PermissionHandler>();

        var adminPanelConnectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

        builder.Services.AddDbContextFactory<ApplicationDbContext>(options =>
            options.UseSqlite(adminPanelConnectionString));

        builder.Services.AddDbContextFactory<EarthDbContext>(options =>
            EarthDbContext.ConfigureBuilder(options, earthDbConnectionString, earthDbProvider));
        builder.Services.AddDatabaseDeveloperPageExceptionFilter();

        builder.Services.AddIdentityCore<ApplicationUser>(options =>
            {
                options.SignIn.RequireConfirmedAccount = true;
                options.Stores.SchemaVersion = IdentitySchemaVersions.Version3;
            })
            .AddRoles<ApplicationRole>()
            .AddEntityFrameworkStores<ApplicationDbContext>()
            .AddSignInManager()
            .AddDefaultTokenProviders();

        builder.Services.AddSingleton<IEmailSender<ApplicationUser>, IdentityNoOpEmailSender>();

        builder.Services.AddControllers()
            .ConfigureApplicationPartManager(manager =>
            {
                manager.FeatureProviders.Add(new InternalControllerFeatureProvider());
            });

        var app = builder.Build();

        var loggerFactory = app.Services.GetRequiredService<ILoggerFactory>();
        GlobalLoggerFactory.Initialize(loggerFactory);

        var programLogger = loggerFactory.CreateLogger(nameof(App));

        // Configure the HTTP request pipeline.
        if (app.Environment.IsDevelopment())
        {
            app.UseMigrationsEndPoint();
        }
        else
        {
            app.UseExceptionHandler("/Error", createScopeForErrors: true);
            // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
            app.UseHsts();
        }

        app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
        app.UseHttpsRedirection();

        app.UseAntiforgery();

        app.MapStaticAssets();
        app.MapRazorComponents<Components.App>()
            .AddInteractiveServerRenderMode();

        // Add additional endpoints required by the Identity /Account Razor components.
        app.MapAdditionalIdentityEndpoints();

        app.MapControllers();

        app.Lifetime.ApplicationStarted.Register(() =>
        {
            var server = app.Services.GetRequiredService<IServer>();
            var addressFeature = server.Features.Get<IServerAddressesFeature>();

            var address = (addressFeature?.Addresses.FirstOrDefault() ?? "").AsSpan();
            var index = address.IndexOf("://");
            if (index != -1)
            {
                address = address[(index + 3)..];
            }

            if (IPEndPoint.TryParse(address, out var endpoint))
            {
                Address = $"http://localhost:{endpoint.Port}";
            }
            else
            {
                Address = "http://localhost:5000";
            }
        });

        // Apply database migrations and initialize built-in roles
        if (!EF.IsDesignTime)
        {
            using (var scope = app.Services.CreateScope())
            {
                // make sure Data dir exists
                Directory.CreateDirectory(Path.Combine(Directory.GetCurrentDirectory(), "Data"));
                // Directory.CreateDirectory(Path.Combine(Directory.GetCurrentDirectory(), Path.GetDirectoryName(Settings.Instance.EarthDatabaseConnectionString)!));

                var appDbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                await appDbContext.Database.MigrateAsync();

                var earthDbContext = scope.ServiceProvider.GetRequiredService<EarthDbContext>();
                await earthDbContext.Database.MigrateAsync();

                var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<ApplicationRole>>();
                var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
                await EnsureBuiltInRolesAsync(roleManager, userManager);

                // todo
                //                 if (isLegacyDb)
                //                 {
                // #pragma warning disable CS0618 // Type or member is obsolete - needed for migration
                //                     var optionsBuilder = new DbContextOptionsBuilder<LiveDbContext>();
                //                     optionsBuilder.UseSqlite("Data Source=" + liveDbPath!);

                //                     using var liveDbContext = new LiveDbContext(optionsBuilder.Options);
                // #pragma warning restore CS0618 // Type or member is obsolete

                //                     await MigrateLegacyDataAsync(earthDbContext, liveDbContext, legacyDbPath);
                //                 }
            }
        }

        // if (args.Any(a => a.StartsWith("--applicationName", StringComparison.Ordinal)))
        // {
        //     await app.RunAsync();
        //     return 0;
        // }
        // init stuff that requires logger but needs to be injected
        var startupDeps = app.Services.GetRequiredService<StartupDependencies>();

        var eventBusConnectionString = builder.Configuration["services:event-bus:raw-tcp:0"];
        Debug.Assert(eventBusConnectionString is not null);
        var eventBusUri = new Uri(eventBusConnectionString);

        LogConnectingToEventBus(programLogger);

        EventBusClient eventBus;
        try
        {
            eventBus = await EventBusClient.ConnectAsync($"{eventBusUri.Host}:{eventBusUri.Port}");
        }
        catch (EventBusClientException exception)
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
        catch (ObjectStoreClientException exception)
        {
            LogConnectToObjectStoreError(programLogger, exception);
            loggerFactory.Dispose();
            return 4;
        }

        LogConnectedToObjectStore(programLogger);

        LogLoadingStaticData(programLogger);
        StaticData.StaticData staticData;
        try
        {
            staticData = new StaticData.StaticData(builder.Configuration["StaticDataPath"]!);
        }
        catch (StaticData.StaticDataException exception)
        {
            LogLoadStaticDataError(programLogger, exception);
            loggerFactory.Dispose();
            return 5;
        }

        LogLoadedStaticData(programLogger);

        startupDeps.EventBus = eventBus;
        startupDeps.ObjectStore = objectStore;
        startupDeps.StaticData = staticData;

        await app.RunAsync();

        return 0;
    }

    private static async Task EnsureBuiltInRolesAsync(RoleManager<ApplicationRole> roleManager, UserManager<ApplicationUser> userManager)
    {
        var everyoneRole = await roleManager.FindByNameAsync(ApplicationRole.Default);

        if (everyoneRole == null)
        {
            everyoneRole = new ApplicationRole
            {
                Name = ApplicationRole.Default,
                Position = int.MaxValue - 10,
                Color = "#99AAB5",
                IsBuiltIn = true
            };
            await roleManager.CreateAsync(everyoneRole);
            await roleManager.AddClaimAsync(everyoneRole, new Claim("Permission", Permissions.LinkPlayers));
        }

        await AssignRoleToAllUsersAsync(userManager, ApplicationRole.Default);

        var ownerRole = await roleManager.FindByNameAsync(ApplicationRole.Owner);

        if (ownerRole == null)
        {
            ownerRole = new ApplicationRole
            {
                Name = ApplicationRole.Owner,
                Position = 0,
                Color = "#FF0000",
                IsBuiltIn = true
            };
            await roleManager.CreateAsync(ownerRole);
        }

        // Sync Permissions
        var currentClaims = await roleManager.GetClaimsAsync(ownerRole);
        var currentPermissionValues = currentClaims
            .Where(c => c.Type == "Permission")
            .Select(c => c.Value)
            .ToHashSet();

        foreach (var permission in Permissions.All)
        {
            if (!currentPermissionValues.Contains(permission))
            {
                // Add the missing permission
                await roleManager.AddClaimAsync(ownerRole, new Claim("Permission", permission));
            }
        }

        // Remove permissions from the Owner that no longer exist in the code
        foreach (var claim in currentClaims.Where(c => c.Type == "Permission"))
        {
            if (!Permissions.All.Contains(claim.Value))
            {
                await roleManager.RemoveClaimAsync(ownerRole, claim);
            }
        }
    }

    private static async Task AssignRoleToAllUsersAsync(UserManager<ApplicationUser> userManager, string roleName)
    {
        foreach (var user in userManager.Users)
        {
            if (!await userManager.IsInRoleAsync(user, roleName))
            {
                await userManager.AddToRoleAsync(user, roleName);
            }
        }
    }

    //     private static async Task<bool> IsLegacyEarthDbAsync(string filePath)
    //     {
    //         if (!File.Exists(filePath))
    //         {
    //             return false;
    //         }

    //         using var connection = new SqliteConnection("Data Source=" + filePath);
    //         await connection.OpenAsync();

    //         using (var command = connection.CreateCommand())
    //         {
    //             command.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name=$name;";
    //             command.Parameters.AddWithValue("$name", "__EFMigrationsHistory");

    //             using (var reader = command.ExecuteReader())
    //             {
    //                 return !reader.HasRows;
    //             }
    //         }
    //     }

    // #pragma warning disable CS0618 // Type or member is obsolete - needed for migration
    //     private static async Task MigrateLegacyDataAsync(EarthDbContext earthDb, LiveDbContext liveDb, string legacyDbPath)
    // #pragma warning restore CS0618 // Type or member is obsolete
    //     {
    //         using var legacyEarthDb = new SqliteConnection("Data Source=" + legacyDbPath);
    //         await legacyEarthDb.OpenAsync();

    //         var migratorLogger = GlobalLoggerFactory.CreateLogger<DatabaseMigrator>();
    //         var migrator = new DatabaseMigrator(earthDb, legacyEarthDb, liveDb, migratorLogger);

    //         Log.Information($"Begining database migration from '{legacyDbPath}' to '{Path.GetFullPath(Settings.Instance.EarthDatabaseConnectionString!)}'");

    //         try
    //         {
    //             await migrator.MigrateAsync();

    //             Log.Information("Database migrated");
    //         }
    //         catch (Exception ex)
    //         {
    //             Log.Error(ex, $"Failed to migrate database. To retry, delete earth.db and rename (earth/live).db.old to (earth/live).db. Error: {ex.Message}");
    //             throw;
    //         }
    //     }

    private sealed class PermissionPolicyProvider(IOptions<AuthorizationOptions> options)
        : DefaultAuthorizationPolicyProvider(options)
    {
        public override async Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
        {
            var policy = await base.GetPolicyAsync(policyName);
            if (policy != null)
            {
                return policy;
            }

            return new AuthorizationPolicyBuilder()
                .AddRequirements(new PermissionRequirement(policyName))
                .Build();
        }
    }

    internal sealed class StartupDependencies
    {
        public EventBusClient EventBus { get; set; } = null!;
        public ObjectStoreClient ObjectStore { get; set; } = null!;
        public StaticData.StaticData StaticData { get; set; } = null!;
    }

    [LoggerMessage(Level = LogLevel.Critical, Message = "Unhandled exception")]
    private static partial void LogUnhandledException(ILogger logger, Exception? exception);

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
}