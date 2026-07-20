namespace Solace.StaticData;

public sealed class StaticDataProvider
{
    public readonly string Directory;

    public StaticDataProvider(string directory)
    {
        Directory = Path.GetFullPath(directory);
    }

    public Catalog Catalog => field ??= new Catalog(Path.Combine(Directory, "catalog"));

    public PlayerLevels Levels => field ??= new PlayerLevels(Path.Combine(Directory, "levels"));

    public TappablesConfig TappablesConfig => field ??= new TappablesConfig(Path.Combine(Directory, "tappables"));

    public EncountersConfig EncountersConfig => field ??= new EncountersConfig(Path.Combine(Directory, "encounters"));

    public TileRenderer TileRenderer => field ??= new TileRenderer(Path.Combine(Directory, "tile_renderer"));

    public Buildplates Buildplates => field ??= new Buildplates(Path.Combine(Directory, "buildplates"));

    public Playfab Playfab => field ??= new Playfab(Path.Combine(Directory, "playfab"));
}