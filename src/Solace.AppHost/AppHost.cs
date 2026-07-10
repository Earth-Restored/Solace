using Google.Protobuf.WellKnownTypes;
using Microsoft.Extensions.Configuration;
using Solace.AppHost;

var builder = DistributedApplication.CreateBuilder(args);

builder.AddDockerComposeEnvironment("solace-prod")
    .WithProperties(env =>
    {
        env.DashboardEnabled = true;
    })
    .WithDashboard(dashboard =>
    {
        dashboard.WithHostPort(80)
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

var earthDbUseSqlite = builder.Configuration.GetValue<bool>("Database:Earth:UseSqlite");

IResourceBuilder<IResourceWithConnectionString> db;
if (earthDbUseSqlite)
{
    db = builder.AddSqlite("EarthDb", builder.Configuration.GetValue("Database:Earth:SqliteDirectory", "data"), builder.Configuration.GetValue("Database:Earth:SqliteFileName", "earth.db"));
}
else
{
    var postgres = builder.AddPostgres("postgres")
        .WithDataVolume()
        .WithPgAdmin();
    db = postgres.AddDatabase("EarthDb");
}

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

var apiPort = builder.Configuration.GetValue<int>("ApiServer:Port", 8088);

var apiServer = builder.AddProject<Projects.Solace_ApiServer>("api-server")
    .WithHttpEndpoint(port: apiPort, targetPort: apiPort, name: "http")
    .WithEndpoint("http", endpoint =>
    {
        endpoint.TargetHost = "*";
    })
    .WithReference(db)
    .WaitFor(db)
    .WithReference(eventBus)
    .WaitFor(eventBus)
    .WithReference(objectStore)
    .WaitFor(objectStore)
    .WithEnvironmentFromSection(builder.Configuration, "ApiServer:Authentication", "ApiServer:")
    .WithEnvironment("StaticDataPath", staticDataPath)
    .PublishAsDockerComposeService((resource, service) =>
    {
        service.Ports = [$"{apiPort}:{apiPort}"];

        service.AddVolume(new Aspire.Hosting.Docker.Resources.ServiceNodes.Volume
        {
            Name = "static-data-volume",
            Type = "bind",
            Source = staticDataPath,
            Target = "/app/static-data",
            ReadOnly = true,
        });

        service.AddVolume(new Aspire.Hosting.Docker.Resources.ServiceNodes.Volume
        {
            Name = "dataprotection-keys-volume",
            Type = "bind",
            Source = "dataprotection-keys",
            Target = "/home/app/.aspnet/DataProtection-Keys",
            ReadOnly = false,
        });

        service.Environment["StaticDataPath"] = "/app/static-data";
    });

if (earthDbUseSqlite)
{
    apiServer.WithEnvironment("DatabaseProvider", "Sqlite");
}
else
{
    apiServer.WithEnvironment("DatabaseProvider", "Postgres");
}

var locatorPort = builder.Configuration.GetValue<int>("Locator:Port", 8088);

var locator = builder.AddProject<Projects.Solace_Locator>("locator")
    .WithHttpEndpoint(port: locatorPort, name: "http")
    .WithEndpoint("http", endpoint =>
    {
        endpoint.TargetHost = "*";
    })
    .WithReference(apiServer)
    .WaitFor(apiServer)
    .PublishAsDockerComposeService((resource, service) =>
    {
        service.Ports = [$"{locatorPort}:{locatorPort}"];
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

var anyTileDataSources = builder.Configuration.GetSection("TileRenderer:TileSource").AsEnumerable().Any(item => !string.IsNullOrWhiteSpace(item.Value));

if (anyTileDataSources)
{
    var tileRenderer = builder.AddProject<Projects.Solace_TileRenderer>("tile-renderer")
        .WithReference(eventBus)
        .WaitFor(eventBus)
        .WithEnvironment("StaticDataPath", staticDataPath)
        .WithEnvironmentFromSection(builder.Configuration, "TileRenderer:TileSource", "TileRenderer:")
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
}

var adminPanelPort = builder.Configuration.GetValue<int>("AdminPanel:Port", 5000);

var adminPanel = builder.AddProject<Projects.Solace_AdminPanel>("admin-panel")
    .WithHttpEndpoint(port: adminPanelPort, name: "http")
    .WithEndpoint("http", endpoint =>
    {
        endpoint.TargetHost = "*";
    })
    .WithReference(db)
    .WaitFor(db)
    .WithReference(eventBus)
    .WaitFor(eventBus)
    .WithReference(objectStore)
    .WaitFor(objectStore)
    .WithEnvironment("StaticDataPath", staticDataPath)
    .WithEnvironment("EnableAdminPanelBuildplatePreview", builder.Configuration["AdminPanel:EnableAdminPanelBuildplatePreview"])
    .PublishAsDockerComposeService((resource, service) =>
    {
        service.Ports = [$"{adminPanelPort}:{adminPanelPort}"];

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

if (earthDbUseSqlite)
{
    adminPanel.WithEnvironment("DatabaseProvider", "Sqlite");
}
else
{
    adminPanel.WithEnvironment("DatabaseProvider", "Postgres");
}

builder.Build().Run();
