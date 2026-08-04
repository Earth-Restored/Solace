using System.Buffers;

namespace Solace.EventBus.Client.Utils;

internal static class StreamUtils
{
    public static async Task SendStreamChunksAsync(Stream sourceStream, Func<ReadOnlyMemory<byte>, bool, CancellationToken, Task> sendChunkFunc, CancellationToken cancellationToken = default)
    {
        const int ChunkSize = 1024 * 32;

        var buffer1 = ArrayPool<byte>.Shared.Rent(ChunkSize);
        var buffer2 = ArrayPool<byte>.Shared.Rent(ChunkSize);

        try
        {
            var currentBuffer = buffer1;
            var nextBuffer = buffer2;

            var currentRead = await sourceStream.ReadAsync(currentBuffer.AsMemory(0, ChunkSize), cancellationToken).ConfigureAwait(false);

            var isEmptyStream = currentRead == 0;

            while (currentRead > 0)
            {
                var nextRead = await sourceStream.ReadAsync(nextBuffer.AsMemory(0, ChunkSize), cancellationToken).ConfigureAwait(false);
                var isLast = nextRead == 0;

                var chunkMemory = currentBuffer.AsMemory(0, currentRead);
                await sendChunkFunc(chunkMemory, isLast, cancellationToken).ConfigureAwait(false);

                currentRead = nextRead;
                (currentBuffer, nextBuffer) = (nextBuffer, currentBuffer);
            }

            if (isEmptyStream)
            {
                await sendChunkFunc(ReadOnlyMemory<byte>.Empty, true, cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer1);
            ArrayPool<byte>.Shared.Return(buffer2);
            await sourceStream.DisposeAsync().ConfigureAwait(false);
        }
    }
}
