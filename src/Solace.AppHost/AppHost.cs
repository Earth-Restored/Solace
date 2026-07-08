using Microsoft.Extensions.Configuration;
using Solace.AppHost;

var builder = DistributedApplication.CreateBuilder(args);

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
    .WithEndpoint(scheme: "tcp", name: "raw-tcp", env: "TCP_PORT");

var objectStoreDataDirectory = Path.GetFullPath(builder.Configuration.GetValue<string>("ObjectStore:DataDirectory", "data"));

var objectStore = builder.AddProject<Projects.Solace_ObjectStore_Server>("object-store")
    .WithHttpEndpoint(name: "http")
    .WithEnvironment("DataDirectory", objectStoreDataDirectory);

var staticDataPath = Path.GetFullPath(builder.Configuration["Shared:StaticDataPath"]!);

var buildplateLauncher = builder.AddProject<Projects.Solace_Buildplate>("buildplate-launcher")
    .WithReference(eventBus)
    .WaitFor(eventBus)
    .WithEnvironmentFromSection(builder.Configuration, "BuildplateLauncher", "BuildplateLauncher:")
    .WithEnvironment("StaticDataPath", staticDataPath);

var apiPort = builder.Configuration.GetValue<int>("ApiServer:Port", 8088);

var apiServer = builder.AddProject<Projects.Solace_ApiServer>("api-server")
    .WithHttpEndpoint(port: apiPort, name: "http")
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
    .WithEnvironment("StaticDataPath", staticDataPath);

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
    .WaitFor(apiServer);

var tappableGenerator = builder.AddProject<Projects.Solace_TappablesGenerator>("tappable-generator")
    .WithReference(eventBus)
    .WaitFor(eventBus)
    .WithEnvironment("StaticDataPath", staticDataPath);

var anyTileDataSources = builder.Configuration.GetSection("TileRenderer:TileSource").AsEnumerable().Any(item => !string.IsNullOrWhiteSpace(item.Value));

if (anyTileDataSources)
{
    var tileRenderer = builder.AddProject<Projects.Solace_TileRenderer>("tile-renderer")
        .WithReference(eventBus)
        .WaitFor(eventBus)
        .WithEnvironment("StaticDataPath", staticDataPath)
        .WithEnvironmentFromSection(builder.Configuration, "TileRenderer:TileSource", "TileRenderer:");
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
    .WithEnvironment("EnableAdminPanelBuildplatePreview", builder.Configuration["AdminPanel:EnableAdminPanelBuildplatePreview"]);

if (earthDbUseSqlite)
{
    adminPanel.WithEnvironment("DatabaseProvider", "Sqlite");
}
else
{
    adminPanel.WithEnvironment("DatabaseProvider", "Postgres");
}

builder.Build().Run();
