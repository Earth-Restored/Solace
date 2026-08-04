using System.Threading.Channels;

namespace Solace.EventBus.Client.Utils;

internal sealed class ChannelStream : Stream
{
    private readonly ChannelReader<ReadOnlyMemory<byte>> _reader;
    private readonly CancellationTokenSource _cts = new();
    private ReadOnlyMemory<byte> _currentChunk;
    private int _currentPosition;
    private bool _disposed;

    public ChannelStream(ChannelReader<ReadOnlyMemory<byte>> reader)
    {
        _reader = reader;
    }

    public override bool CanRead => !_disposed;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => throw new NotSupportedException();
    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token, cancellationToken);
        var token = linkedCts.Token;

        while (_currentPosition >= _currentChunk.Length)
        {
            if (!await _reader.WaitToReadAsync(token).ConfigureAwait(false))
            {
                return 0; // EOF
            }

            if (_reader.TryRead(out var chunk))
            {
                _currentChunk = chunk;
                _currentPosition = 0;
            }
        }

        var bytesToCopy = Math.Min(buffer.Length, _currentChunk.Length - _currentPosition);
        _currentChunk.Slice(_currentPosition, bytesToCopy).CopyTo(buffer);
        _currentPosition += bytesToCopy;
        return bytesToCopy;
    }

    public override int Read(byte[] buffer, int offset, int count)
        => ReadAsync(buffer.AsMemory(offset, count)).AsTask().GetAwaiter().GetResult();

    public override void Flush()
    {
    }

    public override long Seek(long offset, SeekOrigin origin)
        => throw new NotSupportedException();

    public override void SetLength(long value)
        => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count)
        => throw new NotSupportedException();

#pragma warning disable CA2215 // Dispose methods should call base class dispose - base dispose is empty
    protected override void Dispose(bool disposing)
#pragma warning restore CA2215 // Dispose methods should call base class dispose
    {
        if (!_disposed)
        {
            _disposed = true;
            _cts.Cancel();
            _cts.Dispose();
        }
    }

#pragma warning disable CA2215 // Dispose methods should call base class dispose - base dispose is empty
    public override async ValueTask DisposeAsync()
#pragma warning restore CA2215 // Dispose methods should call base class dispose
    {
        if (!_disposed)
        {
            _disposed = true;
            _cts.Cancel();
            _cts.Dispose();
        }
    }
}
