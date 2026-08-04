namespace Solace.Common;

public sealed class FileLock
{
    private readonly FileInfo _file;

    public FileLock(FileInfo file)
    {
        _file = file;
    }

    public async Task<Handle> AcquireAsync(CancellationToken cancellationToken = default)
        => await AcquireAsync(Timeout.InfiniteTimeSpan, TimeSpan.FromMilliseconds(250), cancellationToken);

    public async Task<Handle> AcquireAsync(TimeSpan timeout, CancellationToken cancellationToken = default)
        => await AcquireAsync(timeout, TimeSpan.FromMilliseconds(250), cancellationToken);

    public async Task<Handle> AcquireAsync(TimeSpan timeout, TimeSpan retryInterval, CancellationToken cancellationToken = default)
    {
        var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(timeout);
        var token = cts.Token;

        while (true)
        {
            token.ThrowIfCancellationRequested();

            var handle = TryAcquire(token);

            if (handle is not null)
            {
                return handle.Value;
            }

            await Task.Delay(retryInterval, token);
        }
    }

    public Handle? TryAcquire(CancellationToken cancellationToken = default)
    {
        var retryCount = 0;

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            _file.Directory!.Create();

            FileStream lockFileStream;
            try
            {
                lockFileStream = new FileStream(_file.FullName, FileMode.OpenOrCreate, FileAccess.Read, FileShare.None, bufferSize: 1, FileOptions.DeleteOnClose);
            }
            catch (UnauthorizedAccessException)
            {
                // The file exists and is read-only
                FileAttributes attributes;
                try
                {
                    attributes = _file.Attributes;
                }
                catch
                {
                    attributes = FileAttributes.Normal;
                }

                if (attributes.HasFlag(FileAttributes.ReadOnly))
                {
                    throw new NotSupportedException("Lock file is read-only.");
                }

                if (retryCount < 50)
                {
                    continue;
                }

                throw;
            }

            catch (PathTooLongException)
            {
                throw;
            }
            catch (IOException)
            {
                return null;
            }

            return new Handle(lockFileStream);
        }
    }

    public struct Handle : IDisposable, IAsyncDisposable
    {
        private FileStream? _lock;

        internal Handle(FileStream? @lock)
        {
            _lock = @lock;
        }

        public void Dispose()
            => Interlocked.Exchange(ref _lock, null)?.Dispose();

        public ValueTask DisposeAsync()
        {
            Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
