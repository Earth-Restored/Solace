using System.Collections.Concurrent;
using System.Threading.Channels;
using Google.Protobuf;
using Grpc.Core;
using Solace.EventBus.Client.Utils;

namespace Solace.EventBus.Client;

public sealed class RequestHandler : IAsyncDisposable
{
    private readonly EventBusService.EventBusServiceClient _client;
    private readonly string _queueName;
    private readonly Func<RequestHandlerRequest, CancellationToken, Task<MessagePayload?>> _onRequest;
    private readonly Func<Exception?, Task> _onError;
    private AsyncDuplexStreamingCall<ClientMessage, ServerMessage>? _call;

    private CancellationTokenSource? _cts;
    private Task? _loopTask;
    private readonly TaskCompletionSource _ready = new(TaskCreationOptions.RunContinuationsAsynchronously);

    private SemaphoreSlim? _semaphore;
    private const int MaxDegreeOfParallelism = 4;

    private readonly ConcurrentDictionary<ulong, ChannelWriter<ReadOnlyMemory<byte>>> _activeRequestStreams = new();

    public RequestHandler(
        EventBusService.EventBusServiceClient client,
        string queueName,
        Func<RequestHandlerRequest, CancellationToken, Task<MessagePayload?>> onRequest,
        Func<Exception?, Task> onError)
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
                await foreach (var serverMessage in _call.ResponseStream.ReadAllAsync(_cts.Token))
                {
                    if (serverMessage.HandlerReady)
                    {
                        _ready.TrySetResult();
                        continue;
                    }

                    if (_activeRequestStreams.TryGetValue(serverMessage.CorrelationId, out var existingWriter))
                    {
                        if (serverMessage.PayloadCase is ServerMessage.PayloadOneofCase.BinaryData && !serverMessage.BinaryData.IsEmpty)
                        {
                            await existingWriter.WriteAsync(serverMessage.BinaryData.Memory, _cts.Token);
                        }

                        if (serverMessage.IsLastChunk)
                        {
                            _activeRequestStreams.TryRemove(serverMessage.CorrelationId, out _);
                            existingWriter.TryComplete();
                        }

                        continue;
                    }

                    ChannelReader<ReadOnlyMemory<byte>>? streamReader = null;
                    if (serverMessage.IsStream)
                    {
                        var channel = Channel.CreateUnbounded<ReadOnlyMemory<byte>>(new UnboundedChannelOptions
                        {
                            SingleWriter = false,
                            SingleReader = true
                        });

                        if (serverMessage.PayloadCase is ServerMessage.PayloadOneofCase.BinaryData && !serverMessage.BinaryData.IsEmpty)
                        {
                            channel.Writer.TryWrite(serverMessage.BinaryData.Memory);
                        }

                        if (serverMessage.IsLastChunk)
                        {
                            channel.Writer.TryComplete();
                        }
                        else
                        {
                            _activeRequestStreams[serverMessage.CorrelationId] = channel.Writer;
                        }

                        streamReader = channel.Reader;
                    }

                    _ = Task.Run(async () =>
                    {
                        await _semaphore.WaitAsync(_cts.Token);

                        try
                        {
                            var payload = streamReader is not null
                                ? new MessagePayload(new ChannelStream(streamReader))
                                : serverMessage.PayloadCase == ServerMessage.PayloadOneofCase.BinaryData
                                    ? new MessagePayload(serverMessage.BinaryData.Memory)
                                    : new MessagePayload(serverMessage.StringData);

                            var timestamp = serverMessage.Timestamp?.ToDateTimeOffset() ?? DateTimeOffset.UtcNow;

                            var outData = await _onRequest(new RequestHandlerRequest(timestamp, serverMessage.Type, payload), _cts.Token);

                            if (outData is null || outData.Value.Value is null)
                            {
                                await safeStream.WriteAsync(new ClientMessage
                                {
                                    Response = new HandlerResponse
                                    {
                                        CorrelationId = serverMessage.CorrelationId,
                                        Status = HandlerResponse.Types.Status.NotHandled
                                    }
                                });
                            }
                            else
                            {
                                switch (outData.Value.Value)
                                {
                                    case string valueString:
                                        await safeStream.WriteAsync(new ClientMessage
                                        {
                                            Response = new HandlerResponse
                                            {
                                                CorrelationId = serverMessage.CorrelationId,
                                                Status = HandlerResponse.Types.Status.Success,
                                                StringData = valueString
                                            }
                                        });
                                        break;
                                    case ReadOnlyMemory<byte> valueByte:
                                        await safeStream.WriteAsync(new ClientMessage
                                        {
                                            Response = new HandlerResponse
                                            {
                                                CorrelationId = serverMessage.CorrelationId,
                                                Status = HandlerResponse.Types.Status.Success,
                                                BinaryData = UnsafeByteOperations.UnsafeWrap(valueByte)
                                            }
                                        });
                                        break;
                                    case Stream streamValue:
                                        await StreamUtils.SendStreamChunksAsync(streamValue, async (chunkMemory, isLast, cancellationToken) =>
                                        {
                                            await safeStream.WriteAsync(new ClientMessage
                                            {
                                                Response = new HandlerResponse
                                                {
                                                    CorrelationId = serverMessage.CorrelationId,
                                                    Status = HandlerResponse.Types.Status.Success,
                                                    IsStream = true,
                                                    IsLastChunk = isLast,
                                                    BinaryData = UnsafeByteOperations.UnsafeWrap(chunkMemory)
                                                }
                                            }, cancellationToken: cancellationToken);
                                        }, _cts.Token);
                                        break;
                                }
                            }
                        }
                        catch (Exception exception) when (exception is not (OperationCanceledException or RpcException { StatusCode: StatusCode.Cancelled, }))
                        {
                            try
                            {
                                await safeStream.WriteAsync(new ClientMessage
                                {
                                    Response = new HandlerResponse
                                    {
                                        CorrelationId = serverMessage.CorrelationId,
                                        Status = HandlerResponse.Types.Status.Error,
                                    }
                                });
                            }
                            catch
                            {
                            }

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
                _ready.TrySetException(exception);
                await _onError(exception);
            }
            finally
            {
                _ready.TrySetCanceled();

                foreach (var writer in _activeRequestStreams.Values)
                {
                    writer.TryComplete(new InvalidOperationException("Connection lost before stream completed."));
                }

                _activeRequestStreams.Clear();
            }
        });

        await _ready.Task;
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

            _cts = null;
        }

        _semaphore?.Dispose();
    }
}

#pragma warning disable MA0048 // File name must match type name
public readonly record struct RequestHandlerRequest(DateTimeOffset Timestamp, string Type, MessagePayload Data);
#pragma warning restore MA0048 // File name must match type name
