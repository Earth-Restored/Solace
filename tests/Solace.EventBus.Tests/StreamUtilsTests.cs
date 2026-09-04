using Solace.EventBus.Client.Utils;

namespace Solace.EventBus.Tests;

public class StreamUtilsTests
{
    [Test]
    public async Task SendStreamChunksAsync_LargeStream_SendsChunksAndDisposesStream()
    {
        var totalBytes = 70_000;
        var data = new byte[totalBytes];
        new Random(42).NextBytes(data);

        var stream = new MemoryStream(data);

        var receivedChunks = new List<(byte[] Bytes, bool IsLast)>();

        await StreamUtils.SendStreamChunksAsync(stream, (chunkMemory, isLast, cancellationToken) =>
        {
            receivedChunks.Add((chunkMemory.ToArray(), isLast));
            return Task.CompletedTask;
        });

        await Assert.That(receivedChunks.Count).IsEqualTo(3);

        await Assert.That(receivedChunks[0].Bytes.Length).IsEqualTo(32768);
        await Assert.That(receivedChunks[0].IsLast).IsFalse();

        await Assert.That(receivedChunks[1].Bytes.Length).IsEqualTo(32768);
        await Assert.That(receivedChunks[1].IsLast).IsFalse();

        await Assert.That(receivedChunks[2].Bytes.Length).IsEqualTo(4464);
        await Assert.That(receivedChunks[2].IsLast).IsTrue();

        var reassembled = receivedChunks.SelectMany(c => c.Bytes).ToArray();
        await Assert.That(reassembled).IsEquivalentTo(data);

        Action readFromStream = () => _ = stream.ReadByte();
        await Assert.That(readFromStream).Throws<ObjectDisposedException>();
    }

    [Test]
    public async Task SendStreamChunksAsync_EmptyStream_SendsSingleEmptyLastChunk()
    {
        var stream = new MemoryStream();
        var receivedChunks = new List<(int Length, bool IsLast)>();

        await StreamUtils.SendStreamChunksAsync(stream, (chunkMemory, isLast, cancellationToken) =>
        {
            receivedChunks.Add((chunkMemory.Length, isLast));
            return Task.CompletedTask;
        });

        await Assert.That(receivedChunks.Count).IsEqualTo(1);
        await Assert.That(receivedChunks[0].Length).IsEqualTo(0);
        await Assert.That(receivedChunks[0].IsLast).IsTrue();
    }
}
