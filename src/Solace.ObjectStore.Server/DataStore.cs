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

    public async Task<string> StoreAsync(Stream data, CancellationToken cancellationToken = default)
    {
        string id = Guid.NewGuid().ToString();

        var dir = new DirectoryInfo(Path.Combine(_rootDirectory.FullName, id[..2]));
        if (!dir.Exists)
        {
            dir.Create();
        }

        var file = new FileInfo(Path.Combine(dir.FullName, id));

        try
        {
            using var fileStream = File.OpenWrite(file.FullName);
            await data.CopyToAsync(fileStream, cancellationToken);
        }
        catch (IOException ex)
        {
            file.Delete();
            throw new DataStoreException(ex);
        }

        return id;
    }

    public async Task<(Stream? Stream, long Length)> LoadAsync(string id, CancellationToken cancellationToken = default)
    {
        _ = cancellationToken;

        var file = new FileInfo(Path.Combine(_rootDirectory.FullName, id[..2], id));
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

    public async Task DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        _ = cancellationToken;

        var file = new FileInfo(Path.Combine(_rootDirectory.FullName, id[..2], id));

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
