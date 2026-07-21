using System.Diagnostics;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Solace.Common;
using Solace.WebPortal.Common;
using Solace.WebPortal.Components;
using Solace.WebPortal.Components.Account;
using Solace.WebPortal.Data;
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
internal partial class Program2
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

        var connectionString = builder.Configuration.GetConnectionString("WebPortalDb") ?? (EF.IsDesignTime ? "Host=localhost;Database=dummy;" : throw new InvalidOperationException("Connection string 'WebPortalDb' not found."));

        builder.Services.AddDbContextFactory<ApplicationDbContext>(options =>
            options.UseNpgsql(connectionString));
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

        await using var app = builder.Build();

        var loggerFactory = app.Services.GetRequiredService<ILoggerFactory>();
        GlobalLoggerFactory.Initialize(loggerFactory);

        var programLogger = loggerFactory.CreateLogger(nameof(App));

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
        app.UseHttpsRedirection();

        app.UseAntiforgery();

        app.MapStaticAssets();
        app.MapRazorComponents<App>()
            .AddInteractiveWebAssemblyRenderMode()
            .AddAdditionalAssemblies(typeof(Client._Imports).Assembly);

        // Add additional endpoints required by the Identity /Account Razor components.
        app.MapAdditionalIdentityEndpoints();

        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            await db.Database.MigrateAsync();

            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<ApplicationRole>>();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            await EnsureBuiltInRolesAsync(roleManager, userManager);

            await EnsureOwnerAccountExists(userManager, programLogger);
        }

        app.Run();
    }

    private static async Task EnsureBuiltInRolesAsync(RoleManager<ApplicationRole> roleManager, UserManager<ApplicationUser> userManager)
    {
        var everyoneRole = await roleManager.FindByNameAsync(ApplicationRole.Default);

        if (everyoneRole is null)
        {
            everyoneRole = new ApplicationRole
            {
                Name = ApplicationRole.Default,
                Position = int.MaxValue - 10,
                Color = "#99AAB5",
                IsBuiltIn = true
            };
            await roleManager.CreateAsync(everyoneRole);
            // await roleManager.AddClaimAsync(everyoneRole, new Claim("Permission", Permissions.LinkPlayers));
        }

        await AssignRoleToAllUsersAsync(userManager, ApplicationRole.Default);

        var ownerRole = await roleManager.FindByNameAsync(ApplicationRole.Owner);

        if (ownerRole is null)
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
        var usersInRole = await userManager.GetUsersInRoleAsync(roleName);
        var usersInRoleIds = usersInRole.Select(u => u.Id).ToHashSet();

        var usersWithoutRole = await userManager.Users
            .Where(u => !usersInRoleIds.Contains(u.Id))
            .ToListAsync();

        foreach (var user in usersWithoutRole)
        {
            await userManager.AddToRoleAsync(user, roleName);
        }
    }

    private static async Task EnsureOwnerAccountExists(UserManager<ApplicationUser> userManager, ILogger logger)
    {
        const string OwnerEmail = "admin@solace.com";
        var ownerUser = await userManager.FindByEmailAsync(OwnerEmail);

        if (ownerUser is null)
        {
            var temporaryPassword = GenerateSecurePassword(32);

            ownerUser = new ApplicationUser
            {
                UserName = OwnerEmail,
                Email = OwnerEmail,
                EmailConfirmed = true,
            };

            var createResult = await userManager.CreateAsync(ownerUser, temporaryPassword);

            if (createResult.Succeeded)
            {
                await userManager.AddToRoleAsync(ownerUser, ApplicationRole.Owner);

#pragma warning disable CA1848 // Use the LoggerMessage delegates
                logger.LogWarning("==================================================");
                logger.LogWarning("SETUP: Initial Owner Account Created!");
                logger.LogWarning("Email: {Email}", OwnerEmail);
                logger.LogWarning("Password: {Password}", temporaryPassword);
                logger.LogWarning("PLEASE CHANGE THIS PASSWORD IMMEDIATELY AFTER LOGGING IN.");
                logger.LogWarning("==================================================");
            }
            else
            {
                var errors = string.Join(", ", createResult.Errors.Select(e => e.Description));
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

    [LoggerMessage(Level = LogLevel.Critical, Message = "Unhandled exception")]
    private static partial void LogUnhandledException(ILogger logger, Exception? exception);
}