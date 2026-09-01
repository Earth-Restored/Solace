using System.Net;
using BitcoderCZ.IO;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Solace.ObjectStore.Server;
using Solace.ObjectStore.Server.Services;

namespace Solace.ObjectStore.Tests;

public sealed class TestObjectStoreServer : IAsyncDisposable
{
    private readonly WebApplication _app;
    private readonly AbsoluteDirectory _dataDirectory;

    public string Address { get; }

    private TestObjectStoreServer(WebApplication app, string address, AbsoluteDirectory dataDirectory)
    {
        _app = app;
        Address = address;
        _dataDirectory = dataDirectory;
    }

    public static async Task<TestObjectStoreServer> StartAsync()
    {
        var dataDirectory = new AbsoluteDirectory(Path.GetFullPath(Path.Combine(Path.GetTempPath(), $"solace_os_test_{Guid.NewGuid():N}")));
        dataDirectory.Create();

        var builder = WebApplication.CreateBuilder();
        builder.Logging.SetMinimumLevel(LogLevel.Warning);
        builder.WebHost.ConfigureKestrel(options =>
        {
            options.Listen(IPAddress.Loopback, 0, o => o.Protocols = HttpProtocols.Http2);
        });

        builder.Services.AddGrpc();
        builder.Services.AddSingleton(new DataStore(dataDirectory));

        var app = builder.Build();
        app.MapGrpcService<ObjectStoreServiceImpl>();

        await app.StartAsync();

        var address = app.Services
            .GetRequiredService<IServer>()
            .Features.Get<IServerAddressesFeature>()!
            .Addresses.First();

        return new TestObjectStoreServer(app, address, dataDirectory);
    }

    public async ValueTask DisposeAsync()
    {
        await _app.StopAsync();
        await _app.DisposeAsync();

        if (_dataDirectory.Exists)
        {
            _dataDirectory.Delete(recursive: true);
        }
    }
}
