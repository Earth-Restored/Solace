using System.Diagnostics;
using System.Runtime.CompilerServices;
using Immediate.Handlers.Shared;
using Immediate.Validations.Shared;
using Microsoft.AspNetCore.Authentication.Cookies;

#if USE_SHARED_LIBS
using System.Runtime.Loader;
#endif
using Microsoft.EntityFrameworkCore;
using Solace.AuthServer.Utils;
using Solace.Common;
using Solace.DB;

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

internal sealed partial class Program2
{
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static async Task<int> Run(string[] args)
    {
        var builder = WebApplication.CreateSlimBuilder(args);

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

        builder.Services.Configure<Features.Live.Login.AuthSettings>(builder.Configuration.GetSection("Authentication:Login"));
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

            startupDeps.Secrets = await db.GetOrInitializeSecretsAsync();
        }

        // app.UseHttpsRedirection();

        app.UseStaticFiles();

        app.UseAntiforgery();

        app.MapSolaceAuthServerEndpoints();
        
        app.MapRazorComponents<App>();

        app.Run();

        return 0;
    }

    internal sealed class StartupDependencies
    {
        public CryptoSecrets Secrets { get; set; } = null!;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Using NoOp captcha provider")]
    private static partial void LogUsingNoOpCaptchaProvider(ILogger logger);
}
