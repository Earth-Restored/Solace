using System.Diagnostics;
using System.IO.Compression;
using Microsoft.Extensions.Caching.Memory;
using Solace.StaticData;

namespace Solace.WebPortal.Features.Catalog;

// todo: optimize
public sealed class GenoaResourcepackCache : IDisposable
{
    private readonly StaticDataProvider _staticData;
    private readonly IMemoryCache _cache;
    private readonly SemaphoreSlim _lock = new(1, 1); // Lock does not work with async

    public GenoaResourcepackCache(StaticDataProvider staticData, IMemoryCache cache)
    {
        _staticData = staticData;
        _cache = cache;
    }

    public async Task<byte[]?> GetResourcePackFileAsync(string path, CancellationToken cancellationToken = default)
    {
        var zip = await GetResourcePackAsync();

        await _lock.WaitAsync(cancellationToken);

        try
        {
            var entry = zip.GetEntry(path);

            if (entry is null)
            {
                return null;
            }

            using var entryStream = await entry.OpenAsync(FileAccess.Read, cancellationToken);

            var bytes = new byte[entry.Length];

            await entryStream.ReadExactlyAsync(bytes, cancellationToken);

            return bytes;
        }
        finally
        {
            _lock.Release();
        }
    }

    public void Dispose()
        => _lock.Dispose();

    private async Task<ZipArchive> GetResourcePackAsync()
    {
        var resourcePack = await _cache.GetOrCreateAsync("Genoa_ResourcePack_Zip", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(15);

            return await CreateResourcePackZipAsync();
        });

        Debug.Assert(resourcePack is not null);

        return resourcePack;
    }

    private async Task<ZipArchive?> CreateResourcePackZipAsync()
    {
        var resourcePackPath = _staticData.Resourcepacks.GenoaResourcepackPath;

        if (!File.Exists(resourcePackPath))
        {
            return null;
        }

        using (var outer = await ZipFile.OpenReadAsync(resourcePackPath))
        {
            var innerEntry = outer.GetEntry("genoa.mcpack");

            if (innerEntry is null)
            {
                return null;
            }

            var ms = new MemoryStream();

            using var innerStream = await innerEntry.OpenAsync(FileAccess.Read);

            await innerStream.CopyToAsync(ms);

            return new ZipArchive(ms, ZipArchiveMode.Read);
        }
    }
}