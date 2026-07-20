using Grpc.Core;

namespace Solace.EventBus.Client;

public sealed class RequestHandler : IAsyncDisposable
{
    private readonly EventBusService.EventBusServiceClient _client;
    private readonly string _queueName;
    private readonly Func<RequestHandlerRequest, Task<string?>> _onRequest;
    private readonly Func<Exception?, Task> _onError;
    private AsyncDuplexStreamingCall<ClientMessage, ServerMessage>? _call;

    private CancellationTokenSource? _cts;
    private Task? _loopTask;

    private SemaphoreSlim? _semaphore;
    private const int MaxDegreeOfParallelism = 4;

    public RequestHandler(EventBusService.EventBusServiceClient client, string queueName, Func<RequestHandlerRequest, Task<string?>> onRequest, Func<Exception?, Task> onError)
    {
        _client = client;
        _queueName = queueName;
        _onRequest = onRequest;
        _onError = onError;
    }

    public async Task StartAsync()
    {
        _cts = new CancellationTokenSource();
        _semaphore = new SemaphoreSlim(MaxDegreeOfParallelism);

        _call = _client.HandleRequests(cancellationToken: _cts.Token);

        var safeStream = new SafeStreamWriter<ClientMessage>(_call.RequestStream);

        await safeStream.WriteAsync(new ClientMessage { RegisterQueue = _queueName, });

        _loopTask = Task.Run(async () =>
        {
            try
            {
                await foreach (var serverMsg in _call.ResponseStream.ReadAllAsync(_cts.Token))
                {
                    await _semaphore.WaitAsync(_cts.Token);

                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            var outData = await _onRequest(new RequestHandlerRequest(serverMsg.Timestamp.ToDateTimeOffset(), serverMsg.Type, serverMsg.Data));
                            await safeStream.WriteAsync(new ClientMessage
                            {
                                Response = new HandlerResponse { CorrelationId = serverMsg.CorrelationId, Data = outData ?? "", Status = outData is null ? HandlerResponse.Types.Status.NotHandled : HandlerResponse.Types.Status.Success, }
                            });
                        }
                        catch (Exception exception)
                        {
                            await _onError(exception);

                            try
                            {
                                await safeStream.WriteAsync(new ClientMessage
                                {
                                    Response = new HandlerResponse { CorrelationId = serverMsg.CorrelationId, Status = HandlerResponse.Types.Status.Error, }
                                });
                            }
                            catch
                            {
                            }
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
    }

    public async ValueTask DisposeAsync()
    {
        if (_cts is not null)
        {
            _cts.Cancel();
            try
            {
                if (_call is not null)
                {
                    await _call.RequestStream.CompleteAsync();
                }

                if (_loopTask is not null)
                {
                    await _loopTask;
                }
            }
            catch
            {
            }

            _call?.Dispose();
            _cts.Dispose();
        }

        _semaphore?.Dispose();
    }
}

#pragma warning disable MA0048 // File name must match type name
public readonly record struct RequestHandlerRequest(DateTimeOffset Timestamp, string Type, string Data);
#pragma warning restore MA0048 // File name must match type name
