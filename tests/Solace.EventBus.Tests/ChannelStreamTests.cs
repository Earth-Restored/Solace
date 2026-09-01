using System.Threading.Channels;
using Solace.EventBus.Client.Utils;

namespace Solace.EventBus.Tests;

public sealed class ChannelStreamTests
{
    [Test]
    public async Task Properties_ReflectStreamCapabilities()
    {
        var channel = Channel.CreateUnbounded<ReadOnlyMemory<byte>>();
        using var stream = new ChannelStream(channel.Reader);

        await Assert.That(stream.CanRead).IsTrue();
        await Assert.That(stream.CanSeek).IsFalse();
        await Assert.That(stream.CanWrite).IsFalse();

        Action getLength = () => _ = stream.Length;
        await Assert.That(getLength).Throws<NotSupportedException>();

        Action getPos = () => _ = stream.Position;
        await Assert.That(getPos).Throws<NotSupportedException>();
    }

    [Test]
    public async Task ReadAsync_ReadsSingleChunkCorrectly()
    {
        var channel = Channel.CreateUnbounded<ReadOnlyMemory<byte>>();
        channel.Writer.TryWrite(new byte[] { 1, 2, 3, 4, });
        channel.Writer.TryComplete();

        using var stream = new ChannelStream(channel.Reader);
        var buffer = new byte[10];

        var bytesRead = await stream.ReadAsync(buffer);

        await Assert.That(bytesRead).IsEqualTo(4);
        await Assert.That(buffer[..4]).IsEquivalentTo(new byte[] { 1, 2, 3, 4, });

        var eof = await stream.ReadAsync(buffer);
        await Assert.That(eof).IsEqualTo(0);
    }

    [Test]
    public async Task ReadAsync_ReadsAcrossMultipleChunksAndPartialBuffer()
    {
        var channel = Channel.CreateUnbounded<ReadOnlyMemory<byte>>();
        channel.Writer.TryWrite(new byte[] { 10, 20, 30, });
        channel.Writer.TryWrite(new byte[] { 40, 50, });
        channel.Writer.TryComplete();

        using var stream = new ChannelStream(channel.Reader);

        var buffer = new byte[2];

        var read1 = await stream.ReadAsync(buffer);
        await Assert.That(read1).IsEqualTo(2);
        await Assert.That(buffer).IsEquivalentTo(new byte[] { 10, 20, });

        var read2 = await stream.ReadAsync(buffer);
        await Assert.That(read2).IsEqualTo(1);
        await Assert.That(buffer[..1]).IsEquivalentTo(new byte[] { 30, });

        var read3 = await stream.ReadAsync(buffer);
        await Assert.That(read3).IsEqualTo(2);
        await Assert.That(buffer).IsEquivalentTo(new byte[] { 40, 50, });

        var readEof = await stream.ReadAsync(buffer);
        await Assert.That(readEof).IsEqualTo(0);
    }

    [Test]
    public async Task ReadAsync_AfterDisposed_ThrowsObjectDisposedException()
    {
        var channel = Channel.CreateUnbounded<ReadOnlyMemory<byte>>();
        var stream = new ChannelStream(channel.Reader);
        stream.Dispose();

        var buffer = new byte[10];
        Action action = () => _ = stream.Read(buffer, 0, 10);

        await Assert.That(action).Throws<ObjectDisposedException>();
    }

    [Test]
    public async Task UnsupportedOperations_ThrowNotSupportedException()
    {
        var channel = Channel.CreateUnbounded<ReadOnlyMemory<byte>>();
        using var stream = new ChannelStream(channel.Reader);

        Action seek = () => stream.Seek(0, SeekOrigin.Begin);
        await Assert.That(seek).Throws<NotSupportedException>();

        Action setLength = () => stream.SetLength(10);
        await Assert.That(setLength).Throws<NotSupportedException>();

        Action write = () => stream.Write(new byte[5], 0, 5);
        await Assert.That(write).Throws<NotSupportedException>();
    }
}
