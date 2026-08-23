namespace Solace.StaticData;

public sealed class Buildplates
{
    private const string StoreDirectory = "store";
    private const string LevelDirectory = "level";

    private readonly string _directory;

    internal Buildplates(string dir)
    {
        _directory = dir;

        Directory.CreateDirectory(Path.Combine(_directory, StoreDirectory));
        Directory.CreateDirectory(Path.Combine(_directory, LevelDirectory));
    }

    public IEnumerable<StaticBuidplate> StoreBuildplates => Directory.EnumerateFiles(Path.Combine(_directory, StoreDirectory), "*.zip", SearchOption.TopDirectoryOnly)
        .Select(path => new StaticBuidplate(path));

    public IEnumerable<StaticBuidplate> LevelBuildplates => Directory.EnumerateFiles(Path.Combine(_directory, LevelDirectory), "*.zip", SearchOption.TopDirectoryOnly)
        .Select(path => new StaticBuidplate(path));
}
