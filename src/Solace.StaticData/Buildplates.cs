namespace Solace.StaticData;

public sealed class Buildplates
{
    private const string StoreDirectory = "store";
    private const string LevelDirectory = "level";

    private readonly string _directory;
    private readonly string _storePath;
    private readonly string _levelPath;

    internal Buildplates(string dir)
    {
        _directory = dir;

        _storePath = Path.Combine(_directory, StoreDirectory);
        _levelPath = Path.Combine(_directory, LevelDirectory);
    }

    public IEnumerable<StaticBuidplate> StoreBuildplates => Directory.Exists(_storePath)
        ? Directory.EnumerateFiles(_storePath, "*.zip", SearchOption.TopDirectoryOnly)
            .Select(path => new StaticBuidplate(path))
        : [];

    public IEnumerable<StaticBuidplate> LevelBuildplates => Directory.Exists(_levelPath)
        ? Directory.EnumerateFiles(_levelPath, "*.zip", SearchOption.TopDirectoryOnly)
            .Select(path => new StaticBuidplate(path))
        : [];
}
