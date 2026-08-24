using BitcoderCZ.IO;
using Solace.Common.Utils;

namespace Solace.ObjectStore.Server;

internal sealed class DataStore
{
    private readonly AbsoluteDirectory _rootDirectory;

    public DataStore(AbsoluteDirectory rootDirectory)
    {
        _rootDirectory = rootDirectory;

        if (!_rootDirectory.Exists)
        {
            _rootDirectory.Create();
        }
    }

    public long GetTotalSize(CancellationToken cancellationToken = default)
    {
        if (!_rootDirectory.Exists)
        {
            return 0;
        }

        long result = 0;

        foreach (var subDir in _rootDirectory.EnumerateDirectories(SearchOption.TopDirectoryOnly))
        {
            foreach (var file in subDir.EnumerateFiles(SearchOption.TopDirectoryOnly))
            {
                result += file.Length;
                cancellationToken.ThrowIfCancellationRequested();
            }
        }

        return result;
    }

    public void DeleteAll()
        => _rootDirectory.SafeDelete(recursive: true);

    public async Task<Guid> StoreAsync(Stream data, CancellationToken cancellationToken = default)
    {
        var id = Guid.CreateVersion7();
        var idString = id.ToString();

        var dir = _rootDirectory / idString[..2];
        if (!dir.Exists)
        {
            dir.Create();
        }

        var file = dir / new RelativeFile(idString);

        try
        {
            using var fileStream = file.OpenWrite(true);
            await data.CopyToAsync(fileStream, cancellationToken);
        }
        catch (UnauthorizedAccessException exception)
        {
            file.Delete();
            throw new DataStoreException($"Permission denied writing object file '{file.Value}'", exception);
        }
        catch (IOException exception)
        {
            file.Delete();
            throw new DataStoreException(exception);
        }

        return id;
    }

    public async Task<(Stream? Stream, long Length)> LoadAsync(Guid id, CancellationToken cancellationToken = default)
    {
        _ = cancellationToken;

        var idString = id.ToString();

        var file = _rootDirectory / idString[..2] / new RelativeFile(idString);
        if (!file.Exists)
        {
            return (null, 0);
        }

        try
        {
            return (file.OpenRead(), file.Length);
        }
        catch (IOException exception)
        {
            throw new DataStoreException(exception);
        }
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        _ = cancellationToken;

        var idString = id.ToString();

        var file = _rootDirectory / idString[..2] / new RelativeFile(idString);

        if (file.Exists)
        {
            // throws if parent directory does not exist - guard with Exists
            file.Delete();
        }
    }

    internal sealed class DataStoreException : Exception
    {
        public DataStoreException()
        {
        }

        public DataStoreException(string? message)
            : base(message)
        {
        }

        public DataStoreException(Exception? innerException)
            : base(null, innerException)
        {
        }

        public DataStoreException(string? message, Exception? innerException)
            : base(message, innerException)
        {
        }
    }
}
