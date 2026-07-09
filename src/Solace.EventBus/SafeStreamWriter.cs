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

    public async Task WriteAsync(T message)
    {
        await _semaphore.WaitAsync();
        
        try
        {
            await _writer.WriteAsync(message);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public void Dispose()
        => _semaphore.Dispose();
}