using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Runtime.CompilerServices;
#if USE_SHARED_LIBS
using System.Runtime.Loader;
#endif
using Microsoft.AspNetCore.HttpOverrides;

namespace Solace.Locator;

internal static class Program
{
    private static void Main(string[] args)
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

        App.Run(args);
    }
}

#pragma warning disable MA0048 // File name must match type name
internal sealed partial class App
#pragma warning restore MA0048 // File name must match type name
{
    [MemberNotNullWhen(false, nameof(ApiServerEndPoint))]
    private static bool ApiServerAuto { get; set; }

    private static int ApiServerPort { get; set; }

    private static string? ApiServerEndPoint { get; set; }

    [MemberNotNullWhen(false, nameof(CdnEndPoint))]
    private static bool CdnAuto { get; set; }

    private static int CdnPort { get; set; }

    private static string? CdnEndPoint { get; set; }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void Run(string[] args)
    {
        var builder = WebApplication.CreateSlimBuilder(args);

        builder.AddServiceDefaults();
        builder.WebHost.UseKestrelHttpsConfiguration();

        builder.Services.ConfigureHttpJsonOptions(options =>
        {
            options.SerializerOptions.TypeInfoResolverChain.Insert(0, AppJsonSerializerContext.Default);
        });

        using var app = builder.Build();

        using var loggerFactory = app.Services.GetRequiredService<ILoggerFactory>();

        var programLogger = loggerFactory.CreateLogger(nameof(Program));

        var forwardedHeadersOptions = new ForwardedHeadersOptions
        {
            ForwardedHeaders = ForwardedHeaders.All,
        };

        forwardedHeadersOptions.KnownIPNetworks.Clear();
        forwardedHeadersOptions.KnownProxies.Clear();

        app.UseForwardedHeaders(forwardedHeadersOptions);

        // app.UseHttpsRedirection();

        var apiServerConnectionString = builder.Configuration["services:api-server:http:0"];
        Debug.Assert(apiServerConnectionString is not null);
        var apiServerUri = new Uri(apiServerConnectionString);

        ApiServerPort = apiServerUri.Port;

        var cdnConnectionString = builder.Configuration["services:cdn:http:0"];
        Debug.Assert(cdnConnectionString is not null);
        var cdnUri = new Uri(cdnConnectionString);

        CdnPort = cdnUri.Port;

        var apiServerEndPoint = builder.Configuration["PublicEndpoints:ApiServer"];
        if (!string.IsNullOrWhiteSpace(apiServerEndPoint))
        {
            apiServerEndPoint = apiServerEndPoint.TrimEnd('/');
            if (!apiServerEndPoint.StartsWith("http://", StringComparison.Ordinal) && !apiServerEndPoint.StartsWith("https://", StringComparison.Ordinal))
            {
                LogUriMissingProtocol(programLogger, "api-server", apiServerEndPoint);
                return;
            }

            ApiServerEndPoint = apiServerEndPoint;
            ApiServerAuto = false;
            LogLocatorManualMode(programLogger, "api-server", ApiServerEndPoint);
        }
        else
        {
            ApiServerAuto = true;
            LogLocatorAutoMode(programLogger, "api-server", ApiServerPort);
        }

        var cdnEndPoint = builder.Configuration["PublicEndpoints:Cdn"];
        if (!string.IsNullOrWhiteSpace(cdnEndPoint))
        {
            cdnEndPoint = cdnEndPoint.TrimEnd('/');
            if (!cdnEndPoint.StartsWith("http://", StringComparison.Ordinal) && !cdnEndPoint.StartsWith("https://", StringComparison.Ordinal))
            {
                LogUriMissingProtocol(programLogger, "cdn", cdnEndPoint);
                return;
            }

            CdnEndPoint = cdnEndPoint;
            CdnAuto = false;
            LogLocatorManualMode(programLogger, "cdn", CdnEndPoint);
        }
        else
        {
            CdnAuto = true;
            LogLocatorAutoMode(programLogger, "cdn", CdnPort);
        }

        static EarthApiResponse LocatorHandler(HttpContext context, ILogger<App> logger)
        {
            var protocol = context.Request.IsHttps ? "https://" : "http://";
            var apiServerUri = ApiServerAuto ? $"{protocol}{context.Request.Host.Host}:{ApiServerPort}" : ApiServerEndPoint;
            var cdnUri = CdnAuto ? $"{protocol}{context.Request.Host.Host}:{CdnPort}" : CdnEndPoint;

            LogLocatorIssued(logger, context.Connection.RemoteIpAddress, apiServerUri, cdnUri);

            return new EarthApiResponse(new LocatorResponse(new(StringComparer.Ordinal)
            {
                ["production"] = new LocatorResponse.Environment(apiServerUri, cdnUri, "20CA2"),
            },
            new(StringComparer.Ordinal)
            {
                ["2020.1217.02"] = ["production"],
                ["2020.1210.01"] = ["production"],
            }
            ), new object());
        }

        app.MapGet("/player/environment", LocatorHandler);
        app.MapGet("/api/v1.0/player/environment", LocatorHandler);
        app.MapGet("/api/v1.1/player/environment", LocatorHandler);

        app.MapDefaultEndpoints();

        app.Run();
    }

    [LoggerMessage(Level = LogLevel.Critical, Message = "{Component} public endpoint ({Uri}) is missing protocol, must start with http:// or https://")]
    public static partial void LogUriMissingProtocol(ILogger logger, string Component, string Uri);

    [LoggerMessage(Level = LogLevel.Information, Message = "{Component} using mode manual with uri: {Uri}")]
    public static partial void LogLocatorManualMode(ILogger logger, string Component, string Uri);

    [LoggerMessage(Level = LogLevel.Information, Message = "{Component} using mode auto with port: {Port}")]
    public static partial void LogLocatorAutoMode(ILogger logger, string Component, int Port);

    [LoggerMessage(Level = LogLevel.Information, Message = "{RemoteIp} has issued locator, replying with api: {ApiServerUri}, cdn: {CdnUri}")]
    public static partial void LogLocatorIssued(ILogger logger, IPAddress? RemoteIp, string ApiServerUri, string CdnUri);
}
