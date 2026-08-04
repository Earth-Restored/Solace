using System.Collections.Concurrent;
using System.Threading.Channels;
using Grpc.Core;
using Solace.EventBus.Client.Utils;

namespace Solace.EventBus.Client;

public sealed class Subscriber : IAsyncDisposable
{
    private readonly EventBusService.EventBusServiceClient _client;
    private readonly string _queueName;
    private readonly Func<SubscriberEvent, CancellationToken, Task> _onEvent;
    private readonly Func<Exception?, Task> _onError;

    private CancellationTokenSource? _cts;
    private Task? _loopTask;

    private SemaphoreSlim? _semaphore;
    private const int MaxDegreeOfParallelism = 4;

    private readonly ConcurrentDictionary<ulong, ChannelWriter<ReadOnlyMemory<byte>>> _activeStreams = [];

    internal Subscriber(EventBusService.EventBusServiceClient client, string queueName, Func<SubscriberEvent, CancellationToken, Task> onEvent, Func<Exception?, Task> onError)
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
                    if (msg.StreamId is not 0)
                    {
                        if (_activeStreams.TryGetValue(msg.StreamId, out var existingWriter))
                        {
                            if (msg.PayloadCase is EventMessage.PayloadOneofCase.BinaryData && !msg.BinaryData.IsEmpty)
                            {
                                await existingWriter.WriteAsync(msg.BinaryData.Memory, _cts.Token);
                            }

                            if (msg.IsLastChunk)
                            {
                                _activeStreams.TryRemove(msg.StreamId, out _);
                                existingWriter.TryComplete();
                            }

                            continue;
                        }

                        var channel = Channel.CreateUnbounded<ReadOnlyMemory<byte>>(new UnboundedChannelOptions
                        {
                            SingleWriter = false,
                            SingleReader = true
                        });

                        if (msg.PayloadCase is EventMessage.PayloadOneofCase.BinaryData && !msg.BinaryData.IsEmpty)
                        {
                            channel.Writer.TryWrite(msg.BinaryData.Memory);
                        }

                        if (msg.IsLastChunk)
                        {
                            channel.Writer.TryComplete();
                        }
                        else
                        {
                            _activeStreams[msg.StreamId] = channel.Writer;
                        }

                        await _semaphore.WaitAsync(_cts.Token);

                        var subscriberEvent = new SubscriberEvent(
                            msg.Timestamp.ToDateTimeOffset(),
                            msg.Type,
                            new MessagePayload(new ChannelStream(channel.Reader)));

                        _ = Task.Run(async () =>
                        {
                            try
                            {
                                await _onEvent(subscriberEvent, _cts.Token);
                            }
                            catch (Exception ex)
                            {
                                await _onError(ex);
                            }
                            finally
                            {
                                _semaphore.Release();
                            }
                        });
                    }
                    else
                    {
                        await _semaphore.WaitAsync(_cts.Token);

                        var payload = msg.PayloadCase is EventMessage.PayloadOneofCase.BinaryData
                            ? new MessagePayload(msg.BinaryData.Memory)
                            : new MessagePayload(msg.StringData);

                        var subscriberEvent = new SubscriberEvent(msg.Timestamp.ToDateTimeOffset(), msg.Type, payload);

                        _ = Task.Run(async () =>
                        {
                            try
                            {
                                await _onEvent(subscriberEvent, _cts.Token);
                            }
                            catch (Exception ex)
                            {
                                await _onError(ex);
                            }
                            finally
                            {
                                _semaphore.Release();
                            }
                        });
                    }
                }
            }
            catch (Exception exception) when (exception is not (OperationCanceledException or RpcException { StatusCode: StatusCode.Cancelled }))
            {
                foreach (var writer in _activeStreams.Values)
                {
                    writer.TryComplete(exception);
                }

                await _onError(exception);
            }
            finally
            {
                foreach (var writer in _activeStreams.Values)
                {
                    writer.TryComplete();
                }
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

#pragma warning disable MA0048 // File name must match type name
public readonly record struct SubscriberEvent(DateTimeOffset Timestamp, string Type, MessagePayload Data);
#pragma warning restore MA0048 // File name must match type name
