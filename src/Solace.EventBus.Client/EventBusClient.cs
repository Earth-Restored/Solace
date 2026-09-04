using Grpc.Net.Client;
using Microsoft.Extensions.Logging;

namespace Solace.EventBus.Client;

public sealed partial class EventBusClient : IAsyncDisposable
{
    private readonly GrpcChannel _channel;
    private readonly EventBusService.EventBusServiceClient _client;

    private readonly SemaphoreSlim _subscribersLock = new(1, 1);

    private readonly SemaphoreSlim _handlersLock = new(1, 1);

    private List<Subscriber>? _subscribers;

    private List<RequestHandler>? _handlers;

    private RequestSender RequestSender => field ??= new RequestSender(_client);

    private Publisher Publisher => field ??= new Publisher(_client);

    internal volatile byte _disposed;

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
        if (Interlocked.CompareExchange(ref _disposed, 1, 0) != 0)
        {
            return;
        }

        await _subscribersLock.WaitAsync();
        if (_subscribers is not null)
        {
            foreach (var subscriber in _subscribers)
            {
                await subscriber.DisposeAsync();
            }
        }

        _subscribersLock.Release();
        _subscribersLock.Dispose();

        await _handlersLock.WaitAsync();
        if (_handlers is not null)
        {
            foreach (var handler in _handlers)
            {
                await handler.DisposeAsync();
            }
        }

        _handlersLock.Release();
        _handlersLock.Dispose();

        await _channel.ShutdownAsync();
        _channel.Dispose();
    }

    public async Task<Publisher> AddPublisherAsync()
    {
        ThrowIfDisposed();

        return Publisher;
    }

    public async Task<Subscriber> AddSubscriberAsync(string queueName, Func<SubscriberEvent, CancellationToken, Task> onEvent, Func<Exception?, Task> onError)
    {
        ThrowIfDisposed();

        var subscriber = new Subscriber(_client, queueName, onEvent, onError);

        await _subscribersLock.WaitAsync();

        await subscriber.StartAsync();

        try
        {
            ThrowIfDisposed();

            _subscribers ??= [];
            _subscribers.Add(subscriber);
        }
        catch (ObjectDisposedException)
        {
            await subscriber.DisposeAsync();
            throw;
        }
        finally
        {
            _subscribersLock.Release();
        }

        return subscriber;
    }

    public async Task<RequestSender> AddRequestSenderAsync()
    {
        ThrowIfDisposed();

        return RequestSender;
    }

    public async Task<RequestHandler> AddRequestHandlerAsync(string queueName, Func<RequestHandlerRequest, CancellationToken, Task<MessagePayload?>> onRequest, Func<Exception?, Task> onError)
    {
        ThrowIfDisposed();

        var handler = new RequestHandler(_client, queueName, onRequest, onError);

        await _handlersLock.WaitAsync();

        await handler.StartAsync();

        try
        {
            ThrowIfDisposed();

            _handlers ??= [];
            _handlers.Add(handler);
        }
        catch (ObjectDisposedException)
        {
            await handler.DisposeAsync();
            throw;
        }
        finally
        {
            _handlersLock.Release();
        }

        return handler;
    }

    public Task<bool> PublishAsync(string queueName, string type, string data, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        return Publisher.PublishAsync(queueName, type, data, cancellationToken);
    }

    public Task<bool> PublishAsync(string queueName, string type, byte[] data, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        return Publisher.PublishAsync(queueName, type, data, cancellationToken);
    }

    public Task<bool> PublishAsync(string queueName, string type, Stream stream, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        return Publisher.PublishAsync(queueName, type, stream, cancellationToken);
    }

    public Task<MessagePayload?> RequestAsync(string queueName, string type, string data, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        return RequestSender.RequestAsync(queueName, type, data, cancellationToken);
    }

    public Task<MessagePayload?> RequestAsync(string queueName, string type, byte[] data, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        return RequestSender.RequestAsync(queueName, type, data, cancellationToken);
    }

    public Task<MessagePayload?> RequestAsync(string queueName, string type, Stream stream, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        return RequestSender.RequestAsync(queueName, type, stream, cancellationToken);
    }

    private void ThrowIfDisposed()
        => ObjectDisposedException.ThrowIf(_disposed != 0, this);
}
