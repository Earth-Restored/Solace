using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.Extensions.Logging;

namespace Solace.EventBus.Client;

public sealed partial class EventBusClient : IAsyncDisposable
{
    private readonly GrpcChannel _channel;
    private readonly EventBusService.EventBusServiceClient _client;

    public EventBusClient(GrpcChannel channel, EventBusService.EventBusServiceClient client)
    {
        _channel = channel;
        _client = client;
    }

    public static async Task<EventBusClient> ConnectAsync(string connectionString, ILogger logger)
    {
        _ = logger;

        var channel = GrpcChannel.ForAddress(connectionString);
        var client = new EventBusService.EventBusServiceClient(channel);

        return new EventBusClient(channel, client);
    }

    public async ValueTask DisposeAsync()
    {
        await _channel.ShutdownAsync();
        _channel.Dispose();
    }

    public async Task<Publisher> AddPublisherAsync()
        => new Publisher(_client);

    public async Task<Subscriber> AddSubscriberAsync(string queueName, Func<SubscriberEvent, Task> onEvent, Func<Exception?, Task> onError)
    {
        var subscriber = new Subscriber(_client, queueName, onEvent, onError);
        await subscriber.StartAsync();
        return subscriber;
    }

    public async Task<RequestSender> AddRequestSenderAsync()
        => new RequestSender(_client);

    public async Task<RequestHandler> AddRequestHandlerAsync(string queueName, Func<RequestHandlerRequest, Task<string?>> onRequest, Func<Exception?, Task> onError)
    {
        var handler = new RequestHandler(_client, queueName, onRequest, onError);
        await handler.StartAsync();
        return handler;
    }
}