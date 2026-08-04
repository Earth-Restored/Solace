using System.Buffers;
using Google.Protobuf;

namespace Solace.EventBus.Client;

public sealed class Publisher : IAsyncDisposable
{
    private readonly EventBusService.EventBusServiceClient _client;

    internal Publisher(EventBusService.EventBusServiceClient client)
    {
        _client = client;
    }

    public Task<bool> PublishAsync(string queueName, string type, string data, CancellationToken cancellationToken = default)
        => _client.PublishAsync(new PublishRequest { QueueName = queueName, Type = type, StringData = data, }, cancellationToken: cancellationToken)
            .ResponseAsync.ContinueWith(t => t.Result.Success, TaskContinuationOptions.ExecuteSynchronously);

    public Task<bool> PublishAsync(string queueName, string type, byte[] data, CancellationToken cancellationToken = default)
        => _client.PublishAsync(new PublishRequest { QueueName = queueName, Type = type, BinaryData = UnsafeByteOperations.UnsafeWrap(data), }, cancellationToken: cancellationToken)
            .ResponseAsync.ContinueWith(t => t.Result.Success, TaskContinuationOptions.ExecuteSynchronously);

    public async Task<bool> PublishAsync(string queueName, string type, Stream stream, CancellationToken cancellationToken = default)
    {
        using var call = _client.PublishStream(cancellationToken: cancellationToken);

        await call.RequestStream.WriteAsync(new PublishChunk
        {
            Metadata = new PublishMetadata { QueueName = queueName, Type = type, },
        }, cancellationToken);

        var buffer = ArrayPool<byte>.Shared.Rent(1024 * 32);
        int bytesRead;
        try
        {
            while ((bytesRead = await stream.ReadAsync(buffer, cancellationToken)) > 0)
            {
                await call.RequestStream.WriteAsync(new PublishChunk
                {
                    ChunkData = UnsafeByteOperations.UnsafeWrap(buffer.AsMemory(0, bytesRead))
                }, cancellationToken);
            }

            await call.RequestStream.CompleteAsync();
        }
        finally
        {
            await stream.DisposeAsync();
            ArrayPool<byte>.Shared.Return(buffer);
        }

        var response = await call.ResponseAsync;
        return response.Success;
    }

    public ValueTask DisposeAsync()
        => ValueTask.CompletedTask;
}
