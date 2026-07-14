namespace Solace.ObjectStore.Server;

internal sealed class DataStore
{
    private readonly DirectoryInfo _rootDirectory;

    public DataStore(DirectoryInfo rootDirectory)
    {
        _rootDirectory = rootDirectory;

        if (!_rootDirectory.Exists)
        {
            _rootDirectory.Create();
        }
    }

    public async Task<Guid> StoreAsync(Stream data, CancellationToken cancellationToken = default)
    {
        var id = Guid.CreateVersion7();
        var idString = id.ToString();

        var dir = new DirectoryInfo(Path.Combine(_rootDirectory.FullName, idString[..2]));
        if (!dir.Exists)
        {
            dir.Create();
        }

        var file = new FileInfo(Path.Combine(dir.FullName, idString));

        try
        {
            using var fileStream = File.OpenWrite(file.FullName);
            await data.CopyToAsync(fileStream, cancellationToken);
        }
        catch (UnauthorizedAccessException ex)
        {
            file.Delete();
            throw new DataStoreException($"Permission denied writing object file '{file.FullName}'", ex);
        }
        catch (IOException ex)
        {
            file.Delete();
            throw new DataStoreException(ex);
        }

        return id;
    }

    public async Task<(Stream? Stream, long Length)> LoadAsync(Guid id, CancellationToken cancellationToken = default)
    {
        _ = cancellationToken;

        var idString = id.ToString();

        var file = new FileInfo(Path.Combine(_rootDirectory.FullName, idString[..2], idString));
        if (!file.Exists)
        {
            return (null, 0);
        }

        try
        {
            return (File.OpenRead(file.FullName), file.Length);
        }
        catch (IOException ex)
        {
            throw new DataStoreException(ex);
        }
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        _ = cancellationToken;

        var idString = id.ToString();

        var file = new FileInfo(Path.Combine(_rootDirectory.FullName, idString[..2], idString));

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
