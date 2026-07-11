using System.Diagnostics;
using System.Net;
using System.Runtime.CompilerServices;
#if USE_SHARED_LIBS
using System.Runtime.Loader;
#endif
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.HttpOverrides;

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

internal sealed partial class App
{
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

        var forwardedHeadersOptions = new ForwardedHeadersOptions
        {
            ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto,
        };

        forwardedHeadersOptions.KnownIPNetworks.Clear();
        forwardedHeadersOptions.KnownProxies.Clear();

        app.UseForwardedHeaders(forwardedHeadersOptions);

        // app.UseHttpsRedirection();

        var apiServerConnectionString = builder.Configuration["services:api-server:http:0"];
        Debug.Assert(apiServerConnectionString is not null);
        var apiServerUri = new Uri(apiServerConnectionString);

        var apiServerPort = apiServerUri.Port;

        var cdnConnectionString = builder.Configuration["services:cdn:http:0"];
        Debug.Assert(cdnConnectionString is not null);
        var cdnUri = new Uri(cdnConnectionString);

        var cdnPort = cdnUri.Port;

        EarthApiResponse LocatorHandler(HttpContext context, ILogger<App> logger)
        {
            var protocol = context.Request.IsHttps ? "https://" : "http://";
            var apiServerIP = $"{protocol}{context.Request.Host.Host}:{apiServerPort}";
            var cdnIP = $"{protocol}{context.Request.Host.Host}:{cdnPort}";

            Logs.LogLocatorIssued(logger, context.Connection.RemoteIpAddress, apiServerIP);

            return new EarthApiResponse(new LocatorResponse(new()
            {
                ["production"] = new LocatorResponse.Environment(apiServerIP, cdnIP, "20CA2"),
            },
            new()
            {
                ["2020.1217.02"] = ["production"],
                ["2020.1210.01"] = ["production"],
            }
            ), new object());
        }

        app.MapGet("/player/environment", LocatorHandler);
        app.MapGet("/api/v1.0/player/environment", LocatorHandler);
        app.MapGet("/api/v1.1/player/environment", LocatorHandler);

        app.Run();
    }
}

internal static partial class Logs
{
    [LoggerMessage(Level = LogLevel.Information, Message = "{RemoteIp} has issued locator, replying with {ServerIp}")]
    public static partial void LogLocatorIssued(ILogger logger, IPAddress? RemoteIp, string ServerIp);
}

internal sealed record LocatorResponse(
    Dictionary<string, LocatorResponse.Environment> ServiceEnvironments,
    Dictionary<string, List<string>> SupportedEnvironments
)
{
    internal sealed record Environment(string ServiceUri, string CdnUri, string PlayfabTitleId);
}

internal sealed record EarthApiResponse(LocatorResponse Result, object Updates);

[JsonSerializable(typeof(EarthApiResponse))]
[JsonSerializable(typeof(LocatorResponse))]
[JsonSerializable(typeof(Dictionary<string, LocatorResponse.Environment>))]
[JsonSerializable(typeof(Dictionary<string, List<string>>))]
internal sealed partial class AppJsonSerializerContext : JsonSerializerContext
{
}