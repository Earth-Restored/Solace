using Grpc.Net.Client;
using Microsoft.Extensions.Logging;

namespace Solace.EventBus.Client;

public sealed partial class EventBusClient : IAsyncDisposable
{
    private readonly GrpcChannel _channel;
    private readonly EventBusService.EventBusServiceClient _client;

    private RequestSender RequestSender => field ??= new RequestSender(_client);

    private Publisher Publisher => field ??= new Publisher(_client);

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
        => Publisher;

    public async Task<Subscriber> AddSubscriberAsync(string queueName, Func<SubscriberEvent, CancellationToken, Task> onEvent, Func<Exception?, Task> onError)
    {
        var subscriber = new Subscriber(_client, queueName, onEvent, onError);
        await subscriber.StartAsync();
        return subscriber;
    }

    public async Task<RequestSender> AddRequestSenderAsync()
        => RequestSender;

    public async Task<RequestHandler> AddRequestHandlerAsync(string queueName, Func<RequestHandlerRequest, CancellationToken, Task<MessagePayload?>> onRequest, Func<Exception?, Task> onError)
    {
        var handler = new RequestHandler(_client, queueName, onRequest, onError);
        await handler.StartAsync();
        return handler;
    }

    public Task<bool> PublishAsync(string queueName, string type, string data, CancellationToken cancellationToken = default)
        => Publisher.PublishAsync(queueName, type, data, cancellationToken);

    public Task<bool> PublishAsync(string queueName, string type, byte[] data, CancellationToken cancellationToken = default)
        => Publisher.PublishAsync(queueName, type, data, cancellationToken);

    public Task<bool> PublishAsync(string queueName, string type, Stream stream, CancellationToken cancellationToken = default)
        => Publisher.PublishAsync(queueName, type, stream, cancellationToken);

    public Task<MessagePayload?> RequestAsync(string queueName, string type, string data, CancellationToken cancellationToken = default)
        => RequestSender.RequestAsync(queueName, type, data, cancellationToken);

    public Task<MessagePayload?> RequestAsync(string queueName, string type, byte[] data, CancellationToken cancellationToken = default)
        => RequestSender.RequestAsync(queueName, type, data, cancellationToken);

    public Task<MessagePayload?> RequestAsync(string queueName, string type, Stream stream, CancellationToken cancellationToken = default)
        => RequestSender.RequestAsync(queueName, type, stream, cancellationToken);
}
