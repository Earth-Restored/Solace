using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization.Metadata;
using Immediate.Handlers.Shared;
using Immediate.Validations.Shared;
using Microsoft.AspNetCore.Authentication.Cookies;

#if USE_SHARED_LIBS
using System.Runtime.Loader;
#endif
using Microsoft.EntityFrameworkCore;
using Microsoft.Net.Http.Headers;
using Solace.Common;
using Solace.Common.Asp;
using Solace.Db.Earth;

[assembly: Behaviors(
    typeof(ValidationBehavior<,>)
)]
namespace Solace.AuthServer;

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

        await Program2.Run(args);
    }
}

#pragma warning disable MA0048 // File name must match type name
internal sealed partial class Program2
#pragma warning restore MA0048 // File name must match type name
{
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static async Task<int> Run(string[] args)
    {
        var builder = WebApplication.CreateSlimBuilder(args);

        builder.Services.ConfigureHttpJsonOptions(options =>
        {
            options.SerializerOptions.TypeInfoResolver = new DefaultJsonTypeInfoResolver
            {
                Modifiers = {
                    Common.Asp.Json.ForcePascalCaseAttribute.PascalCaseModifier,
                },
            };
        });

        var earthDbConnectionString = builder.Configuration.GetConnectionString("EarthDb");

        Debug.Assert(earthDbConnectionString is not null);

        builder.Services.AddDbContextFactory<EarthDbContext>(options =>
            EarthDbContext.ConfigureBuilder(options, earthDbConnectionString));

        builder.AddServiceDefaults();
        builder.WebHost.UseKestrelHttpsConfiguration();

        builder.Services.AddHttpClient();
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddAntiforgery();

        // needed for TypedResults.Forbid
        builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie();

        builder.Services.AddRazorComponents();

        builder.Services.AddSolaceAuthServerHandlers();
        builder.Services.AddSolaceAuthServerBehaviors();

        builder.Services.AddSingleton<StartupDependencies>();
        builder.Services.AddSingleton(sp => sp.GetRequiredService<StartupDependencies>().Secrets);
        builder.Services.AddSingleton(sp => sp.GetRequiredService<StartupDependencies>().StaticData);

        builder.Services.AddSingleton<Features.PlayfabApi.Catalog.CatalogService>();

        builder.Services.Configure<Features.Live.Login.AuthSettings>(builder.Configuration.GetSection("Authentication:Login"));
        builder.Services.Configure<Features.XboxLive.AuthSettings>(builder.Configuration.GetSection("Authentication:XboxLive"));
        builder.Services.Configure<Features.PlayfabApi.AuthSettings>(builder.Configuration.GetSection("Authentication:PlayfabApi"));
        builder.Services.Configure<Common.Asp.Captcha.CaptchaOptions>(builder.Configuration.GetSection("Captcha"));

        var captchaProvider = builder.Configuration.GetValue("Captcha:Provider", Common.Asp.Captcha.CaptchaProvider.NoOp);

        switch (captchaProvider)
        {
            case Common.Asp.Captcha.CaptchaProvider.CloudflareTurnstile:
                builder.Services.AddHttpClient<Common.Asp.Captcha.ICaptchaValidator, Common.Asp.Captcha.CloudflareTurnstileValidator>();
                break;
            default:
                builder.Services.AddSingleton<Common.Asp.Captcha.ICaptchaValidator, Common.Asp.Captcha.NoOpCaptchaValidator>();
                break;
        }

        using var app = builder.Build();

        var loggerFactory = app.Services.GetRequiredService<ILoggerFactory>();
        GlobalLoggerFactory.Initialize(loggerFactory);

        var programLogger = loggerFactory.CreateLogger(nameof(Program));

        if (captchaProvider is Common.Asp.Captcha.CaptchaProvider.NoOp)
        {
            LogUsingNoOpCaptchaProvider(programLogger);
        }

        var startupDeps = app.Services.GetRequiredService<StartupDependencies>();

        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<EarthDbContext>();

            await db.Database.MigrateAsync();

            startupDeps.Secrets = await db.GetOrInitializeSecretsAsync();
        }

        LogLoadingStaticData(programLogger);
        StaticData.StaticDataProvider staticData;
        try
        {
            staticData = new(builder.Configuration["StaticDataPath"]!);
        }
        catch (StaticData.StaticDataException exception)
        {
            LogLoadStaticDataError(programLogger, exception);
            loggerFactory.Dispose();
            return 5;
        }

        LogLoadedStaticData(programLogger);

        startupDeps.StaticData = staticData;

        // app.UseHttpsRedirection();

        app.UseStaticFiles(new StaticFileOptions()
        {
            OnPrepareResponse = ctx =>
            {
                if (ctx.File.Name is "master_loc_contents.json")
                {
                    ctx.Context.Response.ContentType = "application/octet-stream";

                    var headers = ctx.Context.Response.Headers;

                    headers[HeaderNames.CacheControl] = "max-age=86312";

                    headers[HeaderNames.Expires] = DateTime.UtcNow.AddDays(1).ToString("R");

                    headers[HeaderNames.AccessControlAllowOrigin] = "*";
                    headers[HeaderNames.AccessControlExposeHeaders] = "x-ms-request-id,Server,x-ms-version,Content-Type,ETag,Last-Modified,x-ms-creation-time,Content-MD5,x-ms-lease-status,x-ms-lease-state,x-ms-blob-type,x-ms-server-encrypted,Accept-Ranges,x-ms-last-access-time,Content-Length,Date,Transfer-Encoding";

                    headers["x-ms-request-id"] = Guid.NewGuid().ToString();
                    headers["x-ms-version"] = "2025-11-05";
                    headers["x-ms-blob-type"] = "BlockBlob";
                    headers["x-ms-server-encrypted"] = "true";

                    headers["Content-MD5"] = "23BzFiCu2jx/FJOAo68/IA==";

                    var nowRfc = DateTime.UtcNow.ToString("R");
                    headers["x-ms-creation-time"] = "Wed, 02 Oct 2024 17:03:16 GMT";
                    headers["x-ms-last-access-time"] = nowRfc;
                    headers["x-ms-lease-status"] = "unlocked";
                    headers["x-ms-lease-state"] = "available";
                }
            },
        });

        app.UseAntiforgery();

        Features.Live.Login.DeviceAddCredential.MapEndpoint(app);
        Features.Live.Login.RST2.MapEndpoint(app);

        app.MapSolaceAuthServerEndpoints();

        app.MapRazorComponents<App>();

        app.Run();

        return 0;
    }

    internal sealed class StartupDependencies
    {
        public Common.Asp.Auth.CryptoSecrets Secrets { get; set; } = null!;
        public StaticData.StaticDataProvider StaticData { get; set; } = null!;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Using NoOp captcha provider")]
    private static partial void LogUsingNoOpCaptchaProvider(ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Loading static data")]
    private static partial void LogLoadingStaticData(ILogger logger);

    [LoggerMessage(Level = LogLevel.Critical, Message = "Failed to load static data")]
    private static partial void LogLoadStaticDataError(ILogger logger, Exception exception);

    [LoggerMessage(Level = LogLevel.Information, Message = "Loaded static data")]
    private static partial void LogLoadedStaticData(ILogger logger);
}
