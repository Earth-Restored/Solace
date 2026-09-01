using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Solace.EventBus.Server.Services;

namespace Solace.EventBus.Tests;

public sealed class TestEventBusServer : IAsyncDisposable
{
    private readonly WebApplication _app;

    public string Address { get; }

    private TestEventBusServer(WebApplication app, string address)
    {
        _app = app;
        Address = address;
    }

    public static async Task<TestEventBusServer> StartAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Logging.SetMinimumLevel(LogLevel.Warning);
        builder.WebHost.ConfigureKestrel(options =>
        {
            options.Listen(IPAddress.Loopback, 0, o => o.Protocols = HttpProtocols.Http2);
        });

        builder.Services.AddGrpc();
        builder.Services.AddSingleton<EventBusServiceImpl.State>();

        var app = builder.Build();
        app.MapGrpcService<EventBusServiceImpl>();

        await app.StartAsync();

        var serverAddresses = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>();
        var address = serverAddresses!.Addresses.First();

        return new TestEventBusServer(app, address);
    }

    public async ValueTask DisposeAsync()
    {
        await _app.StopAsync();
        await _app.DisposeAsync();
    }
}
