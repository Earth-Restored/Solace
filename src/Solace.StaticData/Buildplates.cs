namespace Solace.StaticData;

public sealed class Buildplates
{
    private const string StoreDirectory = "store";

    private readonly string _directory;

    internal Buildplates(string dir)
    {
        _directory = dir;

        Directory.CreateDirectory(Path.Combine(_directory, StoreDirectory));
    }

    public IEnumerable<StaticBuidplate> StoreBuildplates => Directory.EnumerateFiles(Path.Combine(_directory, StoreDirectory))
        .Select(path => new StaticBuidplate(path));
}
