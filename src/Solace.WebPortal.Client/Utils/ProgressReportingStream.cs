namespace Solace.WebPortal.Client.Utils;

public sealed class ProgressReportingStream(Stream innerStream, Action<long> onProgress) : Stream
{
    private long _bytesRead;

    public override bool CanRead => innerStream.CanRead;
    public override bool CanSeek => innerStream.CanSeek;
    public override bool CanWrite => innerStream.CanWrite;
    public override long Length => innerStream.Length;
    public override long Position
    {
        get => innerStream.Position;
        set => innerStream.Position = value;
    }

    public override void Close()
        => innerStream.Close();

    public override void Flush()
        => innerStream.Flush();

    public override async Task FlushAsync(CancellationToken cancellationToken)
        => await innerStream.FlushAsync(cancellationToken);

    public override int Read(byte[] buffer, int offset, int count)
    {
        var bytesRead = innerStream.Read(buffer, offset, count);
        ReportProgress(bytesRead);
        return bytesRead;
    }

    public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        var bytesRead = await innerStream.ReadAsync(buffer.AsMemory(offset, count), cancellationToken);
        ReportProgress(bytesRead);
        return bytesRead;
    }

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        var bytesRead = await innerStream.ReadAsync(buffer, cancellationToken);
        ReportProgress(bytesRead);
        return bytesRead;
    }

    public override int Read(Span<byte> buffer)
    {
        var bytesRead = innerStream.Read(buffer);
        ReportProgress(bytesRead);
        return bytesRead;
    }

    public override int ReadByte()
    {
        var value = innerStream.ReadByte();
        if (value != -1)
        {
            ReportProgress(1);
        }

        return value;
    }

    private void ReportProgress(int bytesRead)
    {
        if (bytesRead > 0)
        {
            _bytesRead += bytesRead;
            onProgress(_bytesRead);
        }
    }

    public override long Seek(long offset, SeekOrigin origin)
        => innerStream.Seek(offset, origin);

    public override void SetLength(long value)
        => innerStream.SetLength(value);

    public override void Write(byte[] buffer, int offset, int count)
        => innerStream.Write(buffer, offset, count);

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            innerStream.Dispose();
        }

        base.Dispose(disposing);
    }

    public override async ValueTask DisposeAsync()
    {
        await innerStream.DisposeAsync();
        await base.DisposeAsync();
    }
}
