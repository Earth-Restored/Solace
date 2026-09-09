namespace Solace.StaticData;

public sealed class Resourcepacks
{
    internal Resourcepacks(string dir)
    {
        var genoaDir = Path.Combine(dir, "genoa");

        if (Directory.Exists(genoaDir))
        {
            var files = Directory.GetFiles(genoaDir, "*", SearchOption.TopDirectoryOnly);

            if (files.Length > 0)
            {
                GenoaResourcepackPath = files[0];
            }
        }
    }

    public string? GenoaResourcepackName => field ??= GenoaResourcepackPath is null ? null : Path.GetFileNameWithoutExtension(GenoaResourcepackPath);

    public string? GenoaResourcepackPath { get; }
}
