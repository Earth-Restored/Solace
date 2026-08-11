using System.Diagnostics;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using OpenIddict.Abstractions;
using Solace.Common;
using Solace.Common.Asp.Oidc;
using Solace.Db;
using Solace.Db.Earth;
using Solace.EventBus.Client;
using Solace.ObjectStore.Client;
using Solace.WebPortal.Common;
using Solace.WebPortal.Common.Features.Roles;
using Solace.WebPortal.Components;
using Solace.WebPortal.Components.Account;
using Solace.WebPortal.Data;
using Solace.WebPortal.Features.Oidc;
#if USE_SHARED_LIBS
using System.Runtime.Loader;
#endif

namespace Solace.WebPortal;

internal static class Program
{
    private static Task Main(string[] args)
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

        return Program2.RunAsync(args);
    }
}

#pragma warning disable MA0048 // File name must match type name
internal sealed partial class Program2
#pragma warning restore MA0048 // File name must match type name
{
    public static async Task RunAsync(string[] args)
    {
        if (!Debugger.IsAttached)
        {
            AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
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

        // Add services to the container.
        builder.Services.AddRazorComponents()
            .AddInteractiveWebAssemblyComponents()
            .AddAuthenticationStateSerialization(options =>
            {
                options.SerializeAllClaims = true;
            });

        builder.Services.AddCascadingAuthenticationState();
        builder.Services.AddScoped<IdentityRedirectManager>();

        builder.Services.AddAuthentication(options =>
            {
                options.DefaultScheme = IdentityConstants.ApplicationScheme;
                options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
            })
            .AddIdentityCookies();
        builder.Services.AddAuthorization();

        var appDbconnectionString = builder.Configuration.GetConnectionString("WebPortalDb") ?? (EF.IsDesignTime ? "Host=localhost;Database=dummy;" : throw new InvalidOperationException("Connection string 'WebPortalDb' not found."));
        var earthDbConnectionString = builder.Configuration.GetConnectionString("EarthDb") ?? (EF.IsDesignTime ? "Host=localhost;Database=dummy;" : throw new InvalidOperationException("Connection string 'EarthDb' not found."));

        builder.Services.AddDbContextFactory<ApplicationDbContext>(options => options.UseNpgsql(appDbconnectionString));
        builder.Services.AddDbContextFactory<EarthDbContext>(options => EarthDbContext.ConfigureBuilder(options, earthDbConnectionString));
        builder.Services.AddDatabaseDeveloperPageExceptionFilter();

        builder.Services.AddIdentityCore<ApplicationUser>(options =>
            {
                // RequireConfirmedEmail
                options.SignIn.RequireConfirmedAccount = true;
                options.Stores.SchemaVersion = IdentitySchemaVersions.Version3;
            })
            .AddRoles<ApplicationRole>()
            .AddEntityFrameworkStores<ApplicationDbContext>()
            .AddSignInManager()
            .AddDefaultTokenProviders();

        builder.Services.AddSingleton<IEmailSender<ApplicationUser>, IdentityNoOpEmailSender>();

        builder.Services.AddAuthorization(options =>
        {
            options.AddPermissionPolicies();
        });

        builder.Services.AddSolaceWebPortalHandlers();
        builder.Services.AddSolaceWebPortalBehaviors();

        builder.Services.Configure<PublicEndpointInfo>(builder.Configuration.GetSection("PublicEndpoints"));

        builder.Services.Configure<Solace.Common.Asp.Captcha.CaptchaConfiguration>(builder.Configuration.GetSection("Captcha"));

        var captchaProvider = builder.Configuration.GetValue("Captcha:Provider", Solace.Common.Asp.Captcha.CaptchaProvider.NoOp);

        switch (captchaProvider)
        {
            case Solace.Common.Asp.Captcha.CaptchaProvider.CloudflareTurnstile:
                builder.Services.AddHttpClient<Solace.Common.Asp.Captcha.ICaptchaValidator, Solace.Common.Asp.Captcha.CloudflareTurnstileValidator>();
                break;
            default:
                builder.Services.AddSingleton<Solace.Common.Asp.Captcha.ICaptchaValidator, Solace.Common.Asp.Captcha.NoOpCaptchaValidator>();
                break;
        }

        builder.Services.AddSingleton<StartupDependencies>();
        builder.Services.AddSingleton(sp => sp.GetRequiredService<StartupDependencies>().EventBus);
        builder.Services.AddSingleton(sp => sp.GetRequiredService<StartupDependencies>().ObjectStore);
        builder.Services.AddSingleton(sp => sp.GetRequiredService<StartupDependencies>().StaticData);

        builder.Services.AddSingleton<Features.Buildplates.BuildplatePreviewGenerationSemaphore>();

        builder.Services.AddMemoryCache();

        builder.Services.AddSingleton<Features.Catalog.CatalogResponseCacheService>();

        builder.Services.AddOpenIddict()
            .AddCore(options =>
            {
                options.UseEntityFrameworkCore()
                    .UseDbContext<ApplicationDbContext>();
            })
            .AddServer(options =>
            {
                var webPortalEndpoint = builder.Configuration["PublicEndpoints:WebPortal"];
                if (string.IsNullOrEmpty(webPortalEndpoint))
                {
                    options.SetIssuer(new Uri($"http://localhost:{builder.Configuration["PORT_SELF"]!}"));
                }
                else
                {
                    options.SetIssuer(new Uri(webPortalEndpoint));
                }

                options.SetAuthorizationEndpointUris("connect/authorize")
                    .SetEndSessionEndpointUris("connect/logout")
                    .SetTokenEndpointUris("connect/token")
                    .SetUserInfoEndpointUris("connect/userinfo");

                options.AllowAuthorizationCodeFlow()
                    .AllowRefreshTokenFlow();

                options.RegisterScopes(
                    OpenIddictConstants.Scopes.OpenId,
                    OpenIddictConstants.Scopes.Email,
                    OpenIddictConstants.Scopes.Profile,
                    OpenIddictConstants.Scopes.Roles);

                var aspNetCoreOptions = options.UseAspNetCore()
                    .EnableAuthorizationEndpointPassthrough()
                    .EnableEndSessionEndpointPassthrough()
                    .EnableTokenEndpointPassthrough()
                    .EnableUserInfoEndpointPassthrough();

                if (builder.Environment.IsDevelopment())
                {
                    aspNetCoreOptions.DisableTransportSecurityRequirement();

                    options.AddEphemeralEncryptionKey()
                        .AddEphemeralSigningKey();
                }
                else
                {
                    var oidcConfig = builder.Configuration.GetSection("Oidc").Get<OidcServerConfiguration>();
                    Debug.Assert(oidcConfig is not null);

                    if (!string.IsNullOrEmpty(oidcConfig.EncryptionCertPath) && File.Exists(oidcConfig.EncryptionCertPath))
                    {
                        var encryptionCert = X509CertificateLoader.LoadPkcs12FromFile(
                            oidcConfig.EncryptionCertPath,
                            password: oidcConfig.EncryptionCertPassword,
                            keyStorageFlags: X509KeyStorageFlags.MachineKeySet
                        );

                        options.AddEncryptionCertificate(encryptionCert);
                    }
                    else
                    {
                        Console.WriteLine("Warning: oidc encryption certificate not provided, using EphemeralEncryptionKey");
                        options.AddEphemeralEncryptionKey();
                    }

                    if (!string.IsNullOrEmpty(oidcConfig.SigningCertPath) && File.Exists(oidcConfig.SigningCertPath))
                    {
                        var signingCert = X509CertificateLoader.LoadPkcs12FromFile(
                            oidcConfig.SigningCertPath,
                            password: oidcConfig.SigningCertPassword,
                            keyStorageFlags: X509KeyStorageFlags.MachineKeySet
                        );

                        options.AddSigningCertificate(signingCert);
                    }
                    else
                    {
                        Console.WriteLine("Warning: oidc signing certificate not provided, using EphemeralSigningKey");
                        options.AddEphemeralSigningKey();
                    }
                }
            })
            .AddValidation(options =>
            {
                options.UseLocalServer();
                options.UseAspNetCore();
            });

        builder.Services.AddHostedService<SeedClientWorker>();

        await using var app = builder.Build();

        var loggerFactory = app.Services.GetRequiredService<ILoggerFactory>();
        GlobalLoggerFactory.Initialize(loggerFactory);

        var programLogger = loggerFactory.CreateLogger(nameof(App));

        if (captchaProvider is Solace.Common.Asp.Captcha.CaptchaProvider.NoOp)
        {
            LogUsingNoOpCaptchaProvider(programLogger);
        }

        // Configure the HTTP request pipeline.
        if (app.Environment.IsDevelopment())
        {
            app.UseWebAssemblyDebugging();
            app.UseMigrationsEndPoint();
        }
        else
        {
            app.UseExceptionHandler("/Error", createScopeForErrors: true);
            // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
            app.UseHsts();
        }

        app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);

        app.UseAntiforgery();

        app.MapStaticAssets();
        app.MapRazorComponents<App>()
            .AddInteractiveWebAssemblyRenderMode()
            .AddAdditionalAssemblies(typeof(Client._Imports).Assembly);

        app.MapSolaceWebPortalEndpoints();

        // Add additional endpoints required by the Identity /Account Razor components.
        app.MapAdditionalIdentityEndpoints();

        app.MapOidcEndpoints();

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
            return;
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
            return;
        }

        LogConnectedToObjectStore(programLogger);

        LogLoadingStaticData(programLogger);
        StaticData.StaticDataProvider staticData;
        try
        {
            staticData = new StaticData.StaticDataProvider(builder.Configuration["StaticDataPath"]!);
        }
        catch (StaticData.StaticDataException exception)
        {
            LogLoadStaticDataError(programLogger, exception);
            loggerFactory.Dispose();
            return;
        }

        LogLoadedStaticData(programLogger);

        startupDeps.EventBus = eventBus;
        startupDeps.ObjectStore = objectStore;
        startupDeps.StaticData = staticData;

        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            await db.Database.MigrateAsyncWithLock();

            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<ApplicationRole>>();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            await EnsureBuiltInRolesAsync(roleManager, userManager);

            await EnsureOwnerAccountExists(userManager, builder.Configuration["AdminAccountPassword"], programLogger);
        }

        app.Run();
    }

    private static async Task EnsureBuiltInRolesAsync(RoleManager<ApplicationRole> roleManager, UserManager<ApplicationUser> userManager)
    {
        var everyoneRole = await roleManager.FindByNameAsync(RoleConstants.Default);

        if (everyoneRole is null)
        {
            everyoneRole = new ApplicationRole
            {
                Name = RoleConstants.Default,
                Position = int.MaxValue - 10,
                Color = "#99AAB5",
                IsBuiltIn = true
            };
            await roleManager.CreateAsync(everyoneRole);
            await roleManager.AddClaimAsync(everyoneRole, new Claim("Permission", Permissions.CreateProfile));
        }

        await AssignRoleToAllUsersAsync(userManager, RoleConstants.Default);

        var ownerRole = await roleManager.FindByNameAsync(RoleConstants.Owner);

        if (ownerRole is null)
        {
            ownerRole = new ApplicationRole
            {
                Name = RoleConstants.Owner,
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
            .ToHashSet(StringComparer.Ordinal);

        foreach (var permission in Permissions.All)
        {
            if (!currentPermissionValues.Contains(permission))
            {
                // Add the missing permission
                await roleManager.AddClaimAsync(ownerRole, new Claim("Permission", permission));
            }
        }

        // Remove permissions from the Owner that no longer exist in the code
        foreach (var claim in currentClaims.Where(static claim => claim.Type is "Permission"))
        {
            if (!Permissions.All.Contains(claim.Value, StringComparer.Ordinal))
            {
                await roleManager.RemoveClaimAsync(ownerRole, claim);
            }
        }
    }

    private static async Task AssignRoleToAllUsersAsync(UserManager<ApplicationUser> userManager, string roleName)
    {
        // todo: optimize
        var users = await userManager.Users.ToListAsync();

        foreach (var user in users)
        {
            if (!await userManager.IsInRoleAsync(user, roleName))
            {
                await userManager.AddToRoleAsync(user, roleName);
            }
        }
    }

    private static async Task EnsureOwnerAccountExists(UserManager<ApplicationUser> userManager, string? newPassword, ILogger logger)
    {
        const string OwnerEmail = "admin@solace.com";
        var ownerUser = await userManager.FindByEmailAsync(OwnerEmail);

        if (ownerUser is null)
        {
            var temporaryPassword = string.IsNullOrWhiteSpace(newPassword) ? GenerateSecurePassword(32) : newPassword;

            ownerUser = new ApplicationUser
            {
                UserName = OwnerEmail,
                Email = OwnerEmail,
                EmailConfirmed = true,
            };

            var createResult = await userManager.CreateAsync(ownerUser, temporaryPassword);

            if (createResult.Succeeded)
            {
                await userManager.AddToRoleAsync(ownerUser, RoleConstants.Owner);

#pragma warning disable CA1848 // Use the LoggerMessage delegates
                logger.LogWarning("==================================================");
                logger.LogWarning("SETUP: Initial owner account created!");
                logger.LogWarning("Email: {Email}", OwnerEmail);
                logger.LogWarning("Password: {Password}", temporaryPassword);
                logger.LogWarning("PLEASE CHANGE THIS PASSWORD IMMEDIATELY AFTER LOGGING IN.");
                logger.LogWarning("==================================================");
            }
            else
            {
                var errors = string.Join(", ", createResult.Errors.Select(e => e.Description));
                logger.LogError("Failed to create initial owner user: {Errors}", errors);
            }
        }
        else if (!string.IsNullOrWhiteSpace(newPassword))
        {
            if (await userManager.HasPasswordAsync(ownerUser))
            {
                await userManager.RemovePasswordAsync(ownerUser);
            }

            var updateResult = await userManager.AddPasswordAsync(ownerUser, newPassword);

            if (updateResult.Succeeded)
            {
                logger.LogWarning("==================================================");
                logger.LogWarning("Initial owner account password updated!");
                logger.LogWarning("Email: {Email}", OwnerEmail);
                logger.LogWarning("Password: {Password}", newPassword);
                logger.LogWarning("PLEASE CHANGE THIS PASSWORD IMMEDIATELY AFTER LOGGING IN.");
                logger.LogWarning("==================================================");
            }
            else
            {
                var errors = string.Join(", ", updateResult.Errors.Select(e => e.Description));
                logger.LogError("Failed to create initial owner user: {Errors}", errors);
#pragma warning restore CA1848 // Use the LoggerMessage delegates
            }
        }
    }

    private static string GenerateSecurePassword(int length)
    {
        const string validChars = "ABCDEFGHJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789!@#$%^&*()_-+=";
        var randomBytes = new byte[length];

        RandomNumberGenerator.Fill(randomBytes);

        var chars = new char[length];
        for (var i = 0; i < length; i++)
        {
            chars[i] = validChars[randomBytes[i] % validChars.Length];
        }

        return new string(chars);
    }

    internal sealed class StartupDependencies
    {
        public EventBusClient EventBus { get; set; } = null!;
        public ObjectStoreClient ObjectStore { get; set; } = null!;
        public StaticData.StaticDataProvider StaticData { get; set; } = null!;
    }

    [LoggerMessage(Level = LogLevel.Critical, Message = "Unhandled exception")]
    private static partial void LogUnhandledException(ILogger logger, Exception? exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Using NoOp captcha provider")]
    private static partial void LogUsingNoOpCaptchaProvider(ILogger logger);

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