using System.IO.Compression;

namespace Solace.LauncherUI;

internal static class GenoaResourcepackCache
{
    public static async Task<string?> GetCachePath()
    {
        var cachePath = Path.GetFullPath(Path.Combine(Program.StaticDataDir, "resourcepacks", "genoa_cache"));

        if (Directory.Exists(cachePath))
        {
            return cachePath;
        }

        var resourcepackFilePath = Path.GetFullPath(Path.Combine(Program.StaticDataDir, "resourcepacks", "vanilla.zip"));

        if (!File.Exists(resourcepackFilePath))
        {
            return null;
        }

        using (var outerZip = await ZipFile.OpenReadAsync(resourcepackFilePath))
        {
            var innerZipEntry = outerZip.GetEntry("genoa.mcpack");

            if (innerZipEntry is null)
            {
                return null;
            }

            using (var innerZip = new ZipArchive(await innerZipEntry.OpenAsync(), ZipArchiveMode.Read, false))
            {
                await innerZip.ExtractToDirectoryAsync(cachePath);
            }
        }

        return cachePath;
    }
}