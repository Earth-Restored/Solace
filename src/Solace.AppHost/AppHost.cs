using Microsoft.Extensions.Configuration;
using Solace.AppHost;

#pragma warning disable MA0048 // File name must match type name
var builder = DistributedApplication.CreateBuilder(args);
#pragma warning restore MA0048 // File name must match type name

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

builder.AddContainer("nginx", "nginx", "alpine")
    .WithHttpEndpoint(port: 80, targetPort: 80, name: "http")
    .WithHttpsEndpoint(port: 443, targetPort: 443, name: "https")
    .WithExternalHttpEndpoints()
    .WithBindMount("./nginx.conf", "/etc/nginx/nginx.conf", isReadOnly: true)
    .WithBindMount("./certs", "/etc/nginx/certs", isReadOnly: true)
    .WithLifetime(ContainerLifetime.Persistent);

var postgres = builder.AddPostgres("postgres")
    .WithDataVolume()
    .WithPgAdmin();
var earthDb = postgres.AddDatabase("EarthDb");

var eventBus = builder.AddProject<Projects.Solace_EventBus_Server>("event-bus")
    .WithHttpEndpoint(name: "http");

var objectStoreDataDirectory = builder.Configuration.GetValue<string>("ObjectStore:DataDirectory", "data/object_store");
if (builder.Configuration.GetValue<bool>("Shared:ResolvePaths", false))
{
    objectStoreDataDirectory = Path.GetFullPath(objectStoreDataDirectory);
}

var objectStore = builder.AddProject<Projects.Solace_ObjectStore_Server>("object-store")
    .WithHttpEndpoint(name: "http")
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

var staticDataPath = builder.Configuration["Shared:StaticDataPath"]!;
if (builder.Configuration.GetValue<bool>("Shared:ResolvePaths", false))
{
    staticDataPath = Path.GetFullPath(staticDataPath);
}

var buildplateLauncher = builder.AddProject<Projects.Solace_Buildplate>("buildplate-launcher")
    .WithReference(eventBus)
    .WaitFor(eventBus)
    .WithEnvironmentFromSection(builder.Configuration, "BuildplateLauncher", "BuildplateLauncher:")
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

var apiPort = builder.Configuration.GetValue<int>("ApiServer:Port", 8089);

var apiServer = builder.AddProject<Projects.Solace_ApiServer>("api-server")
    .WithHttpEndpoint(port: apiPort, name: "http")
    .WithEndpoint("http", endpoint =>
    {
        endpoint.TargetHost = "*";
    })
    .WithReference(earthDb)
    .WaitFor(earthDb)
    .WithReference(eventBus)
    .WaitFor(eventBus)
    .WithReference(objectStore)
    .WaitFor(objectStore)
    .WithEnvironmentFromSection(builder.Configuration, "AuthServer:Authentication", "AuthServer:")
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

var cdn = builder.AddProject<Projects.Solace_Cdn>("cdn")
    .WithHttpEndpoint(port: cdnPort, name: "http")
    .WithEndpoint("http", endpoint =>
    {
        endpoint.TargetHost = "*";
    })
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

var captchaProvider = builder.AddParameterForEnvironment("Shared:Captcha:Provider", defaultValue: "NoOp", prefixToRemove: "Shared:");
var captchaCloudflareTurnstileSiteKey = builder.AddParameterForEnvironment("Shared:Captcha:CloudflareTurnstileSiteKey", prefixToRemove: "Shared:");
var captchaCloudflareTurnstileSecretKey = builder.AddParameterForEnvironment("Shared:Captcha:CloudflareTurnstileSecretKey", prefixToRemove: "Shared:", isSecret: true);

var authServer = builder.AddProject<Projects.Solace_AuthServer>("auth-server")
    .WithHttpEndpoint(port: authServerPort, name: "http")
    .WithEndpoint("http", endpoint =>
    {
        endpoint.TargetHost = "*";
    })
    .WithReference(earthDb)
    .WaitFor(earthDb)
    .WithEnvironmentFromSection(builder.Configuration, "AuthServer:Authentication", "AuthServer:")
    .WithEnvironmentFromConfig(captchaProvider)
    .WithEnvironmentFromConfig(captchaCloudflareTurnstileSiteKey)
    .WithEnvironmentFromConfig(captchaCloudflareTurnstileSecretKey)
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

var apiServerPublicEndPoint = builder.AddParameterForEnvironment("Shared:PublicEndpoints:ApiServer", prefixToRemove: "Shared:");
var cdnPublicEndPoint = builder.AddParameterForEnvironment("Shared:PublicEndpoints:Cdn", prefixToRemove: "Shared:");

var locator = builder.AddProject<Projects.Solace_Locator>("locator")
    .WithHttpEndpoint(port: locatorPort, name: "http")
    .WithEndpoint("http", endpoint =>
    {
        endpoint.TargetHost = "*";
    })
    .WithReference(apiServer)
    .WaitFor(apiServer)
    .WithReference(cdn)
    .WaitFor(cdn)
    .WithEnvironmentFromConfig(apiServerPublicEndPoint)
    .WithEnvironmentFromConfig(cdnPublicEndPoint)
    .PublishAsDockerComposeService((resource, service) =>
    {
    });

var tappableGenerator = builder.AddProject<Projects.Solace_TappablesGenerator>("tappable-generator")
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

var tileRenderer = builder.AddProject<Projects.Solace_TileRenderer>("tile-renderer")
    .WithReference(eventBus)
    .WaitFor(eventBus)
    .WithEnvironment("StaticDataPath", staticDataPath)
    .WithEnvironment("TileSource__TileJsonUrl", builder.AddParameter("TileRenderer-TileSource-TileJsonUrl", () => builder.Configuration["TileRenderer:TileSource:TileJsonUrl"] ?? ""))
    .WithEnvironment("TileSource__TileDatabaseConnectionString", builder.AddParameter("TileRenderer-TileSource-TileDatabaseConnectionString", () => builder.Configuration["TileRenderer:TileSource:TileDatabaseConnectionString"] ?? ""))
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

var adminPanelPort = builder.Configuration.GetValue<int>("AdminPanel:Port", 5000);

var locatorPublicEndPoint = builder.AddParameterForEnvironment("Shared:PublicEndpoints:Locator", prefixToRemove: "Shared:");
var authServerPublicEndPoint = builder.AddParameterForEnvironment("Shared:PublicEndpoints:AuthServer", prefixToRemove: "Shared:");

var adminPanel = builder.AddProject<Projects.Solace_AdminPanel>("admin-panel")
    .WithHttpEndpoint(port: adminPanelPort, name: "http")
    .WithEndpoint("http", endpoint =>
    {
        endpoint.TargetHost = "*";
    })
    .WithReference(earthDb)
    .WaitFor(earthDb)
    .WithReference(eventBus)
    .WaitFor(eventBus)
    .WithReference(objectStore)
    .WaitFor(objectStore)
    .WithEnvironment("StaticDataPath", staticDataPath)
    .WithEnvironment("EnableAdminPanelBuildplatePreview", builder.Configuration["AdminPanel:EnableAdminPanelBuildplatePreview"])
    .WithEnvironmentFromConfig(captchaProvider)
    .WithEnvironmentFromConfig(captchaCloudflareTurnstileSiteKey)
    .WithEnvironmentFromConfig(captchaCloudflareTurnstileSecretKey)
    .WithEnvironmentFromConfig(locatorPublicEndPoint)
    .WithEnvironmentFromConfig(authServerPublicEndPoint)
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
            Name = "data-volume",
            Type = "bind",
            Source = "data",
            Target = "/app/data",
            ReadOnly = false,
        });

        service.AddVolume(new Aspire.Hosting.Docker.Resources.ServiceNodes.Volume
        {
            Name = "dataprotection-keys-volume",
            Type = "bind",
            Source = "dataprotection-keys",
            Target = "/home/app/.aspnet/DataProtection-Keys",
            ReadOnly = false,
        });
    });

builder.Build().Run();
