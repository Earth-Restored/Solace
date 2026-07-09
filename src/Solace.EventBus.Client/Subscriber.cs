using Grpc.Core;

namespace Solace.EventBus.Client;

public sealed class Subscriber : IAsyncDisposable
{
    private readonly EventBusService.EventBusServiceClient _client;
    private readonly string _queueName;
    private readonly Func<SubscriberEvent, Task> _onEvent;
    private readonly Func<Exception?, Task> _onError;

    private CancellationTokenSource? _cts;
    private Task? _loopTask;

    private SemaphoreSlim? _semaphore;
    private const int MaxDegreeOfParallelism = 4;

    internal Subscriber(EventBusService.EventBusServiceClient client, string queueName, Func<SubscriberEvent, Task> onEvent, Func<Exception?, Task> onError)
    {
        _client = client;
        _queueName = queueName;
        _onEvent = onEvent;
        _onError = onError;
    }

    public Task StartAsync()
    {
        _cts = new CancellationTokenSource();
        _semaphore = new SemaphoreSlim(MaxDegreeOfParallelism);

        var streamCall = _client.Subscribe(new SubscribeRequest { QueueName = _queueName }, cancellationToken: _cts.Token);

        _loopTask = Task.Run(async () =>
        {
            try
            {
                await foreach (var msg in streamCall.ResponseStream.ReadAllAsync(_cts.Token))
                {
                    await _semaphore.WaitAsync(_cts.Token);

                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await _onEvent(new SubscriberEvent(msg.Timestamp.ToDateTimeOffset(), msg.Type, msg.Data));
                        }
                        catch (Exception exception)
                        {
                            await _onError(exception);
                        }
                        finally
                        {
                            _semaphore.Release();
                        }
                    });
                }
            }
            catch (Exception exception) when (exception is not (OperationCanceledException or RpcException { StatusCode: StatusCode.Cancelled, }))
            {
                await _onError(exception);
            }
        });
        return Task.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        if (_cts is not null)
        {
            _cts.Cancel();

            if (_loopTask is not null)
            {
                try
                {
                    await _loopTask;
                }
                catch
                {
                }
            }

            _cts.Dispose();
        }

        _semaphore?.Dispose();
    }
}

public readonly record struct SubscriberEvent(DateTimeOffset Timestamp, string Type, string Data);
