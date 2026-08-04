using Grpc.Core;

namespace Solace.EventBus;

public sealed class SafeStreamWriter<T> : IDisposable
{
    private readonly IAsyncStreamWriter<T> _writer;
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    public SafeStreamWriter(IAsyncStreamWriter<T> writer)
    {
        _writer = writer;
    }

    public async Task WriteAsync(T message, CancellationToken cancellationToken = default)
    {
        await _semaphore.WaitAsync(cancellationToken);

        try
        {
            await _writer.WriteAsync(message, cancellationToken);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public void Dispose()
        => _semaphore.Dispose();
}