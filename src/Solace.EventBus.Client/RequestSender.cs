using System.Threading.Channels;
using Google.Protobuf;
using Grpc.Core;
using Solace.EventBus.Client.Utils;

namespace Solace.EventBus.Client;

public sealed class RequestSender : IAsyncDisposable
{
    private readonly EventBusService.EventBusServiceClient _client;

    internal RequestSender(EventBusService.EventBusServiceClient client)
    {
        _client = client;
    }

    public Task<MessagePayload?> RequestAsync(string queueName, string type, string data, CancellationToken cancellationToken = default)
        => RequestInternalAsync(new RequestMessage { QueueName = queueName, Type = type, StringData = data, }, cancellationToken);

    public Task<MessagePayload?> RequestAsync(string queueName, string type, byte[] data, CancellationToken cancellationToken = default)
        => RequestInternalAsync(new RequestMessage { QueueName = queueName, Type = type, BinaryData = UnsafeByteOperations.UnsafeWrap(data), }, cancellationToken);

    public async Task<MessagePayload?> RequestAsync(string queueName, string type, Stream stream, CancellationToken cancellationToken = default)
    {
        var call = _client.RequestStream(cancellationToken: cancellationToken);

        await call.RequestStream.WriteAsync(new RequestChunk
        {
            Metadata = new RequestMetadata
            {
                QueueName = queueName,
                Type = type,
                IsStream = true,
            }
        }, cancellationToken);

        _ = Task.Run(async () =>
        {
            try
            {
                await StreamUtils.SendStreamChunksAsync(stream, async (chunkMemory, isLast, cancellationToken) =>
                {
                    await call.RequestStream.WriteAsync(new RequestChunk
                    {
                        IsLastChunk = isLast,
                        ChunkData = UnsafeByteOperations.UnsafeWrap(chunkMemory),
                    }, cancellationToken);
                }, cancellationToken);

                await call.RequestStream.CompleteAsync();
            }
            catch
            {
            }
        }, cancellationToken);

        return await ReadResponseStreamAsync(call, cancellationToken);
    }

    public ValueTask DisposeAsync()
        => ValueTask.CompletedTask;

    private async Task<MessagePayload?> RequestInternalAsync(RequestMessage requestMessage, CancellationToken cancellationToken)
    {
        var response = await _client.RequestAsync(requestMessage, cancellationToken: cancellationToken).ResponseAsync;

        return response.Status switch
        {
            ResponseMessage.Types.Status.Success => response.PayloadCase is ResponseMessage.PayloadOneofCase.BinaryData
                ? new MessagePayload(response.BinaryData.Memory)
                : new MessagePayload(response.StringData),
            ResponseMessage.Types.Status.NoHandlers => (MessagePayload?)null,
#pragma warning disable CA2201 // Do not raise reserved exception types
            _ => throw new Exception(response.ErrorMessage),
#pragma warning restore CA2201 // Do not raise reserved exception types
        };
    }

    private static async Task<MessagePayload?> ReadResponseStreamAsync(
        AsyncDuplexStreamingCall<RequestChunk, ResponseChunk> call,
        CancellationToken cancellationToken)
    {
        var responseStream = call.ResponseStream;
        if (!await responseStream.MoveNext(cancellationToken))
        {
            throw new RpcException(new Status(StatusCode.Internal, "No response received from server."));
        }

        var firstMsg = responseStream.Current;

        if (firstMsg.Status is ResponseMessage.Types.Status.NoHandlers)
        {
            return null;
        }

        if (firstMsg.Status is not ResponseMessage.Types.Status.Success)
        {
#pragma warning disable CA2201 // Do not raise reserved exception types
            throw new Exception(firstMsg.ErrorMessage);
#pragma warning restore CA2201 // Do not raise reserved exception types
        }

        if (firstMsg.IsStream)
        {
            var channel = Channel.CreateUnbounded<ReadOnlyMemory<byte>>(new UnboundedChannelOptions
            {
                SingleWriter = true,
                SingleReader = true,
            });

            if (firstMsg.PayloadCase is ResponseChunk.PayloadOneofCase.BinaryData && !firstMsg.BinaryData.IsEmpty)
            {
                channel.Writer.TryWrite(firstMsg.BinaryData.Memory);
            }

            if (firstMsg.IsLastChunk)
            {
                channel.Writer.TryComplete();
            }
            else
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        while (await responseStream.MoveNext(cancellationToken))
                        {
                            var chunkMsg = responseStream.Current;
                            if (chunkMsg.PayloadCase is ResponseChunk.PayloadOneofCase.BinaryData && !chunkMsg.BinaryData.IsEmpty)
                            {
                                await channel.Writer.WriteAsync(chunkMsg.BinaryData.Memory, cancellationToken);
                            }

                            if (chunkMsg.IsLastChunk)
                            {
                                break;
                            }
                        }

                        channel.Writer.TryComplete();
                    }
                    catch (Exception ex)
                    {
                        channel.Writer.TryComplete(ex);
                    }
                }, cancellationToken);
            }

            return new MessagePayload(new ChannelStream(channel.Reader));
        }

        return firstMsg.PayloadCase switch
        {
            ResponseChunk.PayloadOneofCase.BinaryData => new MessagePayload(firstMsg.BinaryData.Memory),
            ResponseChunk.PayloadOneofCase.StringData => new MessagePayload(firstMsg.StringData),
            _ => (MessagePayload?)null
        };
    }
}
