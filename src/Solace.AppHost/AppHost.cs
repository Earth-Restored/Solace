using System.Globalization;
using System.Net;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Solace.AppHost;

#pragma warning disable MA0048 // File name must match type name
var builder = DistributedApplication.CreateBuilder(args);
#pragma warning restore MA0048 // File name must match type name

var migrationMode = builder.Configuration.GetValue<bool>("migration-mode");
var testMode = builder.Configuration.GetValue<bool>("TestMode");

builder.AddDockerComposeEnvironment("solace-prod")
    .WithProperties(env =>
    {
        env.DashboardEnabled = true;
    })
    .WithDashboard(dashboard =>
    {
        dashboard.WithHostPort(5000)
            .WithForwardedHeaders(enabled: true);
    })
    .ConfigureComposeFile(file =>
    {
        if (file.Services.TryGetValue("solace-prod-dashboard", out var dashboardSvc))
        {
            dashboardSvc.Environment["Dashboard__Otlp__AuthMode"] = "ApiKey";
            dashboardSvc.Environment["Dashboard__Otlp__PrimaryApiKey"] = "${DASHBOARD_OTLP_PRIMARY_APIKEY}";
        }

        foreach (var service in file.Services.Values)
        {
            if (service.Environment.ContainsKey("OTEL_EXPORTER_OTLP_ENDPOINT"))
            {
                service.Environment["OTEL_EXPORTER_OTLP_HEADERS"] = "x-otlp-api-key=${DASHBOARD_OTLP_PRIMARY_APIKEY}";
            }
        }
    });

var postgres = builder.AddPostgres("postgres")
    .WithArgs("-c", "max_connections=500")
    .WithPgAdmin();

if (builder.Configuration.GetValue("UseVolumes", true))
{
    var volumeName = builder.Configuration.GetValue<string?>("PostgresVolumeName", null);
    postgres.WithDataVolume(string.IsNullOrWhiteSpace(volumeName) ? null : volumeName);
}

var earthDb = postgres.AddDatabase("EarthDb");
var playfabDb = postgres.AddDatabase("PlayfabDb");
var webPortalDb = postgres.AddDatabase("WebPortalDb");

var objectStoreDataDirectory = builder.Configuration.GetValue<string>("ObjectStore:DataDirectory", "data/object_store");
if (builder.Configuration.GetValue<bool>("Shared:ResolvePaths", false))
{
    objectStoreDataDirectory = Path.GetFullPath(objectStoreDataDirectory);
}

var objectStore = builder.AddDotnetProject("object-store", "../Solace.ObjectStore.Server")
    .WithHttpEndpoint(name: "http")
    .WithHealthCheck("object-store-health-check")
    .WithEnvironment("DataDirectory", objectStoreDataDirectory)
    .PublishAsDockerComposeService((resource, service) =>
    {
        service.AddVolume(new Aspire.Hosting.Docker.Resources.ServiceNodes.Volume
        {
            Name = "object-store-volume",
            Type = "bind",
            Source = objectStoreDataDirectory,
            Target = "/app/data/object_store",
            ReadOnly = false,
        });

        service.Environment["DataDirectory"] = "/app/data/object_store";
    });

builder.Services.AddHealthChecks()
    .AddAsyncCheck("object-store-health-check", async (cancellationToken) =>
    {
        var endpoint = objectStore.GetEndpoint("http");
        var healthUrl = $"{endpoint.Url}/health";

        using var client = new HttpClient();
        var request = new HttpRequestMessage(HttpMethod.Get, healthUrl)
        {
            Version = HttpVersion.Version20,
            VersionPolicy = HttpVersionPolicy.RequestVersionExact
        };

        try
        {
            var response = await client.SendAsync(request, cancellationToken);
            return response.IsSuccessStatusCode
                ? HealthCheckResult.Healthy()
                : HealthCheckResult.Unhealthy($"Returned HTTP {(int)response.StatusCode}");
        }
        catch (Exception exception)
        {
            return HealthCheckResult.Unhealthy("HTTP/2 health probe failed", exception);
        }
    });

if (migrationMode)
{
    builder.Build().Run();
    return;
}

var eventBus = builder.AddDotnetProject("event-bus", "../Solace.EventBus.Server")
    .WithHttpEndpoint(name: "http")
    .WithHealthCheck("event-bus-health-check");

builder.Services.AddHealthChecks()
    .AddAsyncCheck("event-bus-health-check", async (cancellationToken) =>
    {
        var endpoint = eventBus.GetEndpoint("http");
        var healthUrl = $"{endpoint.Url}/health";

        using var client = new HttpClient();
        var request = new HttpRequestMessage(HttpMethod.Get, healthUrl)
        {
            Version = HttpVersion.Version20,
            VersionPolicy = HttpVersionPolicy.RequestVersionExact
        };

        try
        {
            var response = await client.SendAsync(request, cancellationToken);
            return response.IsSuccessStatusCode
                ? HealthCheckResult.Healthy()
                : HealthCheckResult.Unhealthy($"Returned HTTP {(int)response.StatusCode}");
        }
        catch (Exception exception)
        {
            return HealthCheckResult.Unhealthy("HTTP/2 health probe failed", exception);
        }
    });

var staticDataPath = builder.Configuration["Shared:StaticDataPath"]!;
if (builder.Configuration.GetValue<bool>("Shared:ResolvePaths", false))
{
    staticDataPath = Path.GetFullPath(staticDataPath);
}

var acceptMinecraftEula = builder.AddConfigParameter("Shared:AcceptMinecraftEula");

var buildplateServerSetup = builder.AddDotnetProject("buildplate-server-setup", "../Solace.Buildplate.ServerSetup")
    .WithEnvironmentParameter(acceptMinecraftEula, prefixToRemove: "Shared:")
    .WithEnvironment("StaticDataPath", staticDataPath)
    .PublishAsDockerComposeService((resource, service) =>
    {
        service.AddVolume(new Aspire.Hosting.Docker.Resources.ServiceNodes.Volume
        {
            Name = "static-data-volume",
            Type = "bind",
            Source = staticDataPath,
            Target = "/app/static-data",
            ReadOnly = false,
        });

        service.Environment["StaticDataPath"] = "/app/static-data";
    });

var buildplateLauncherPublicEndpoint = builder.AddConfigParameter("BuildplateLauncher:PublicEndpoint");

var buildplateLauncher = builder.AddDotnetProject("buildplate-launcher", "../Solace.Buildplate.Launcher")
    .WithHttpEndpoint(name: "http")
    .WithHttpHealthCheck("/health")
    .WithReference(eventBus)
    .WaitFor(eventBus)
    .WithReference(buildplateServerSetup)
    .WaitFor(buildplateServerSetup)
    .WithEnvironmentSection(builder.Configuration,
        "BuildplateLauncher",
        prefixToRemove: "BuildplateLauncher:",
        parameterOverrides: buildplateLauncherPublicEndpoint)
    .WithEnvironmentParameter(acceptMinecraftEula, prefixToRemove: "Shared:")
    .WithEnvironment("StaticDataPath", staticDataPath)
    .PublishAsDockerComposeService((resource, service) =>
    {
        service.AddVolume(new Aspire.Hosting.Docker.Resources.ServiceNodes.Volume
        {
            Name = "static-data-volume",
            Type = "bind",
            Source = staticDataPath,
            Target = "/app/static-data",
            ReadOnly = false,
        });

        service.Environment["StaticDataPath"] = "/app/static-data";
    });

var buildplateUpdater = builder.AddDotnetProject("buildplate-updater", "../Solace.Buildplate.Updater")
    .WithHttpEndpoint(name: "http")
    .WithHttpHealthCheck("/health")
    .WithReference(eventBus)
    .WaitFor(eventBus)
    .WithReference(buildplateServerSetup)
    .WaitFor(buildplateServerSetup)
    .WithEnvironmentParameter(acceptMinecraftEula, prefixToRemove: "Shared:")
    .WithEnvironment("StaticDataPath", staticDataPath)
    .PublishAsDockerComposeService((resource, service) =>
    {
        service.AddVolume(new Aspire.Hosting.Docker.Resources.ServiceNodes.Volume
        {
            Name = "static-data-volume",
            Type = "bind",
            Source = staticDataPath,
            Target = "/app/static-data",
            ReadOnly = false,
        });

        service.Environment["StaticDataPath"] = "/app/static-data";
    });

var apiPort = builder.Configuration.GetValue<int>("ApiServer:Port", 8089);

var fixUpBuildplatesOnImport = builder.AddConfigParameter("Shared:FixUpBuildplatesOnImport");

var apiServer = builder.AddDotnetProject("api-server", "../Solace.ApiServer")
    .WithHttpEndpoint(port: apiPort, name: "http")
    .WithEndpoint("http", endpoint =>
    {
        endpoint.TargetHost = "*";
    })
    .WithHttpHealthCheck("/health")
    .WithReference(earthDb)
    .WaitFor(earthDb)
    .WithReference(playfabDb)
    .WaitFor(playfabDb)
    .WithReference(eventBus)
    .WaitFor(eventBus)
    .WithReference(objectStore)
    .WaitFor(objectStore);

if (!testMode)
{
    apiServer
        .WithReference(buildplateLauncher)
        .WaitFor(buildplateLauncher);
}

apiServer
    .WithEnvironmentSection(builder.Configuration, "ApiServer:Authentication", prefixToRemove: "ApiServer:")
    .WithEnvironmentParameter(fixUpBuildplatesOnImport, prefixToRemove: "Shared:")
    .WithEnvironment("StaticDataPath", staticDataPath)
    .PublishAsDockerComposeService((resource, service) =>
    {
        service.AddVolume(new Aspire.Hosting.Docker.Resources.ServiceNodes.Volume
        {
            Name = "static-data-volume",
            Type = "bind",
            Source = staticDataPath,
            Target = "/app/static-data",
            ReadOnly = true,
        });

        service.Environment["StaticDataPath"] = "/app/static-data";

        service.AddVolume(new Aspire.Hosting.Docker.Resources.ServiceNodes.Volume
        {
            Name = "dataprotection-keys-volume",
            Type = "bind",
            Source = "dataprotection-keys",
            Target = "/home/app/.aspnet/DataProtection-Keys",
            ReadOnly = false,
        });
    });

var cdnPort = builder.Configuration.GetValue<int>("Cdn:Port", 8090);

var cdn = builder.AddDotnetProject("cdn", "../Solace.Cdn")
    .WithHttpEndpoint(port: cdnPort, name: "http")
    .WithEndpoint("http", endpoint =>
    {
        endpoint.TargetHost = "*";
    })
    .WithHttpHealthCheck("/health")
    .WithReference(earthDb)
    .WaitFor(earthDb)
    .WithReference(eventBus)
    .WaitFor(eventBus)
    .WithReference(objectStore)
    .WaitFor(objectStore)
    .WithEnvironment("StaticDataPath", staticDataPath)
    .PublishAsDockerComposeService((resource, service) =>
    {
        service.AddVolume(new Aspire.Hosting.Docker.Resources.ServiceNodes.Volume
        {
            Name = "static-data-volume",
            Type = "bind",
            Source = staticDataPath,
            Target = "/app/static-data",
            ReadOnly = true,
        });

        service.Environment["StaticDataPath"] = "/app/static-data";
    });

var authServerPort = builder.Configuration.GetValue<int>("AuthServer:Port", 8088);
var webPortalPort = builder.Configuration.GetValue<int>("WebPortal:Port", 5000);

var webPortalPublicEndpoint = builder.AddConfigParameter("Shared:PublicEndpoints:WebPortal", isOptional: true);
var authServerOidcClientSecret = builder.AddConfigParameter("Shared:Oidc:WebPortal:AuthServer:ClientSecret", isSecret: true);
var authServerOidcAllowInsecure = builder.AddConfigParameter("Shared:Oidc:WebPortal:AuthServer:AllowInsecure");
var captchaProvider = builder.AddConfigParameter("Shared:Captcha:Provider", defaultValue: "NoOp");
var captchaCloudflareTurnstileSiteKey = builder.AddConfigParameter("Shared:Captcha:CloudflareTurnstileSiteKey", isOptional: true);
var captchaCloudflareTurnstileSecretKey = builder.AddConfigParameter("Shared:Captcha:CloudflareTurnstileSecretKey", isSecret: true, isOptional: true);

// todo: seperate playfab into it's own project
var authServer = builder.AddDotnetProject("auth-server", "../Solace.AuthServer")
    .WithHttpEndpoint(port: authServerPort, name: "http")
    .WithEndpoint("http", endpoint =>
    {
        endpoint.TargetHost = "*";
    })
    .WithHttpHealthCheck("/health")
    .WithReference(earthDb)
    .WaitFor(earthDb)
    .WithReference(playfabDb)
    .WaitFor(playfabDb)
    .WithReference(eventBus)
    .WaitFor(eventBus)
    .WithEnvironmentSection(builder.Configuration, "AuthServer:Authentication", prefixToRemove: "AuthServer:")
    .WithEnvironmentSection(builder.Configuration, "Shared:Oidc:WebPortal:AuthServer",
        prefixToRemove: "Shared:Oidc:WebPortal:AuthServer:",
        prefixToAdd: "Oidc:",
        authServerOidcClientSecret,
        authServerOidcAllowInsecure)
    .WithEnvironmentParameter(webPortalPublicEndpoint, prefixToRemove: "Shared:")
    .WithEnvironmentParameter(captchaProvider, prefixToRemove: "Shared:")
    .WithEnvironmentParameter(captchaCloudflareTurnstileSiteKey, prefixToRemove: "Shared:")
    .WithEnvironmentParameter(captchaCloudflareTurnstileSecretKey, prefixToRemove: "Shared:")
    .WithEnvironment("StaticDataPath", staticDataPath)
    .PublishAsDockerComposeService((resource, service) =>
    {
        service.AddVolume(new Aspire.Hosting.Docker.Resources.ServiceNodes.Volume
        {
            Name = "dataprotection-keys-volume",
            Type = "bind",
            Source = "dataprotection-keys",
            Target = "/home/app/.aspnet/DataProtection-Keys",
            ReadOnly = false,
        });

        service.AddVolume(new Aspire.Hosting.Docker.Resources.ServiceNodes.Volume
        {
            Name = "static-data-volume",
            Type = "bind",
            Source = staticDataPath,
            Target = "/app/static-data",
            ReadOnly = true,
        });

        service.Environment["StaticDataPath"] = "/app/static-data";
    });

var locatorPort = builder.Configuration.GetValue<int>("Locator:Port", 8080);

var apiServerPublicEndpoint = builder.AddConfigParameter("Shared:PublicEndpoints:ApiServer", isOptional: true);
var cdnPublicEndpoint = builder.AddConfigParameter("Shared:PublicEndpoints:Cdn", isOptional: true);

var locator = builder.AddDotnetProject("locator", "../Solace.Locator")
    .WithHttpEndpoint(port: locatorPort, name: "http")
    .WithEndpoint("http", endpoint =>
    {
        endpoint.TargetHost = "*";
    })
    .WithHttpHealthCheck("/health")
    .WithReference(apiServer)
    .WaitFor(apiServer)
    .WithReference(cdn)
    .WaitFor(cdn)
    .WithEnvironmentParameter(apiServerPublicEndpoint, prefixToRemove: "Shared:")
    .WithEnvironmentParameter(cdnPublicEndpoint, prefixToRemove: "Shared:")
    .PublishAsDockerComposeService((resource, service) =>
    {
    });

var tappableGenerator = builder.AddDotnetProject("tappable-generator", "../Solace.TappablesGenerator")
    .WithHttpEndpoint(name: "http")
    .WithHttpHealthCheck("/health")
    .WithReference(eventBus)
    .WaitFor(eventBus)
    .WithEnvironment("StaticDataPath", staticDataPath)
    .PublishAsDockerComposeService((resource, service) =>
    {
        service.AddVolume(new Aspire.Hosting.Docker.Resources.ServiceNodes.Volume
        {
            Name = "static-data-volume",
            Type = "bind",
            Source = staticDataPath,
            Target = "/app/static-data",
            ReadOnly = true,
        });

        service.Environment["StaticDataPath"] = "/app/static-data";
    });

var tileJsonUrl = builder.AddConfigParameter("TileRenderer:TileSource:TileJsonUrl", isOptional: true);
var tileDbConnString = builder.AddConfigParameter("TileRenderer:TileSource:TileDatabaseConnectionString", isOptional: true);

var tileRenderer = builder.AddDotnetProject("tile-renderer", "../Solace.TileRenderer")
    .WithHttpEndpoint(name: "http")
    .WithHttpHealthCheck("/health")
    .WithReference(eventBus)
    .WaitFor(eventBus)
    .WithEnvironment("StaticDataPath", staticDataPath)
    .WithEnvironmentParameter(tileJsonUrl, prefixToRemove: "TileRenderer:")
    .WithEnvironmentParameter(tileDbConnString, prefixToRemove: "TileRenderer:")
    .PublishAsDockerComposeService((resource, service) =>
    {
        service.AddVolume(new Aspire.Hosting.Docker.Resources.ServiceNodes.Volume
        {
            Name = "static-data-volume",
            Type = "bind",
            Source = staticDataPath,
            Target = "/app/static-data",
            ReadOnly = true,
        });

        service.Environment["StaticDataPath"] = "/app/static-data";
    });

var locatorPublicEndpoint = builder.AddConfigParameter("Shared:PublicEndpoints:Locator", isOptional: true);
var authServerPublicEndpoint = builder.AddConfigParameter("Shared:PublicEndpoints:AuthServer", isOptional: true);

var adminAccountPassword = builder.AddConfigParameter("WebPortal:AdminAccountPassword", isSecret: true, isOptional: true);

var buildplatePreviewEnabled = builder.AddConfigParameter("WebPortal:BuildplatePreview:Enabled");
var buildplatePreviewGenerationMaxConcurrency = builder.AddConfigParameter("WebPortal:BuildplatePreview:GenerationMaxConcurrency");

var webportalOidcSigningCertPassword = builder.AddConfigParameter("Shared:Oidc:WebPortal:SigningCertPassword", isSecret: true, isOptional: true);
var webportalOidcEncryptionCertPassword = builder.AddConfigParameter("Shared:Oidc:WebPortal:EncryptionCertPassword", isSecret: true, isOptional: true);

var webPortal = builder.AddDotnetProject("web-portal", "../Solace.WebPortal")
    .WithHttpEndpoint(port: webPortalPort, name: "http")
    .WithEndpoint("http", endpoint =>
    {
        endpoint.TargetHost = "*";
    })
    .WithHttpHealthCheck("/health")
    .WithReference(authServer)
    .WaitFor(authServer)
    .WithReference(earthDb)
    .WaitFor(earthDb)
    .WithReference(playfabDb)
    .WaitFor(playfabDb)
    .WithReference(webPortalDb)
    .WaitFor(webPortalDb)
    .WithReference(eventBus)
    .WaitFor(eventBus)
    .WithReference(objectStore)
    .WaitFor(objectStore)
    .WithEnvironment("PORT_SELF", webPortalPort.ToString(CultureInfo.InvariantCulture))
    .WithEnvironment("StaticDataPath", staticDataPath)
    .WithEnvironmentSection(builder.Configuration, "Shared:Oidc:WebPortal",
        prefixToRemove: "Shared:Oidc:WebPortal:",
        prefixToAdd: "Oidc:",
        authServerOidcClientSecret,
        authServerOidcAllowInsecure,
        webportalOidcSigningCertPassword,
        webportalOidcEncryptionCertPassword)
    .WithEnvironmentParameter(captchaProvider, prefixToRemove: "Shared:")
    .WithEnvironmentParameter(captchaCloudflareTurnstileSiteKey, prefixToRemove: "Shared:")
    .WithEnvironmentParameter(captchaCloudflareTurnstileSecretKey, prefixToRemove: "Shared:")
    .WithEnvironmentParameter(webPortalPublicEndpoint, prefixToRemove: "Shared:")
    .WithEnvironmentParameter(locatorPublicEndpoint, prefixToRemove: "Shared:")
    .WithEnvironmentParameter(authServerPublicEndpoint, prefixToRemove: "Shared:")
    .WithEnvironmentParameter(adminAccountPassword, prefixToRemove: "WebPortal:")
    .WithEnvironmentParameter(buildplatePreviewEnabled, prefixToRemove: "WebPortal:")
    .WithEnvironmentParameter(buildplatePreviewGenerationMaxConcurrency, prefixToRemove: "WebPortal:")
    .WithEnvironmentParameter(fixUpBuildplatesOnImport, prefixToRemove: "Shared:")
    .PublishAsDockerComposeService((resource, service) =>
    {
        service.AddVolume(new Aspire.Hosting.Docker.Resources.ServiceNodes.Volume
        {
            Name = "static-data-volume",
            Type = "bind",
            Source = staticDataPath,
            Target = "/app/static-data",
            ReadOnly = true,
        });

        service.Environment["StaticDataPath"] = "/app/static-data";

        service.AddVolume(new Aspire.Hosting.Docker.Resources.ServiceNodes.Volume
        {
            Name = "oidc-signing-cert-volume",
            Type = "bind",
            Source = builder.Configuration["Shared:Oidc:WebPortal:SigningCertPath"],
            Target = "/app/certs/oidc-signing-cert.pfx",
            ReadOnly = true,
        });

        service.Environment["Oidc:SigningCertPath"] = "/app/certs/oidc-signing-cert.pfx";

        service.AddVolume(new Aspire.Hosting.Docker.Resources.ServiceNodes.Volume
        {
            Name = "oidc-encryption-cert-volume",
            Type = "bind",
            Source = builder.Configuration["Shared:Oidc:WebPortal:EncryptionCertPath"],
            Target = "/app/certs/oidc-encryption-cert.pfx",
            ReadOnly = true,
        });

        service.Environment["Oidc:EncryptionCertPath"] = "/app/certs/oidc-encryption-cert.pfx";

        service.AddVolume(new Aspire.Hosting.Docker.Resources.ServiceNodes.Volume
        {
            Name = "dataprotection-keys-volume",
            Type = "bind",
            Source = "dataprotection-keys",
            Target = "/home/app/.aspnet/DataProtection-Keys",
            ReadOnly = false,
        });
    });

authServer.WithReference(webPortal);

builder.AddContainer("nginx", "nginx", "alpine")
    .WithExternalHttpEndpoints()
    .WaitFor(webPortal)
    .WaitFor(locator)
    .WaitFor(authServer)
    .WaitFor(apiServer)
    .WaitFor(cdn)
    .WithBindMount("./nginx.conf", "/etc/nginx/nginx.conf", isReadOnly: true)
    .WithBindMount("./certs", "/etc/nginx/certs", isReadOnly: true)
    .WithLifetime(ContainerLifetime.Persistent);

var app = builder.Build();

await app.RunAsync();
