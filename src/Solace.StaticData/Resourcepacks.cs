namespace Solace.StaticData;

public sealed class Resourcepacks
{
    public string GenoaDirectory { get; }

    public string GenoaCacheDirectory { get; }

    public string? GenoaResourcepackPath { get; }

    public string? GenoaResourcepackName => field ??= GenoaResourcepackPath is null ? null : Path.GetFileNameWithoutExtension(GenoaResourcepackPath);

    internal Resourcepacks(string dir)
    {
        GenoaDirectory = Path.Combine(dir, "genoa");
        GenoaCacheDirectory = Path.Combine(dir, "genoa_cache");

        Directory.CreateDirectory(GenoaCacheDirectory);

        if (Directory.Exists(GenoaDirectory))
        {
            var files = Directory.GetFiles(GenoaDirectory, "*", SearchOption.TopDirectoryOnly);

            if (files.Length > 0)
            {
                GenoaResourcepackPath = files[0];
            }
        }
    }

    public string GetCachedResourcepackPath(Guid id)
        => Path.Combine(GenoaCacheDirectory, id.ToString());

    public string? FindResourcepackPath(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        var cachedPath = Path.Combine(GenoaCacheDirectory, name);
        if (File.Exists(cachedPath))
        {
            return cachedPath;
        }

        if (string.Equals(GenoaResourcepackName, name, StringComparison.OrdinalIgnoreCase))
        {
            return GenoaResourcepackPath;
        }

        return null;
    }
}
