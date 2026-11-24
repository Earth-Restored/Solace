using System.IO.Compression;
using Microsoft.EntityFrameworkCore;
using Solace.Db.Earth;
using Solace.EventBus.Client;
using Solace.StaticData;

namespace Solace.WebPortal.Features.Catalog;

public sealed partial class GenoaResourcepackCacheService : IDisposable
{
    private readonly StaticDataProvider _staticData;
    private readonly IDbContextFactory<EarthDbContext> _earthDbFactory;
    private readonly ILogger<GenoaResourcepackCacheService> _logger;
    private readonly SemaphoreSlim _lock = new(1, 1);

    private ZipArchive? _cachedZip;
    private bool _loaded;
    private DateTimeOffset _expiresAt;
    private Subscriber? _subscriber;

    public GenoaResourcepackCacheService(
        StaticDataProvider staticData,
        IDbContextFactory<EarthDbContext> earthDbFactory,
        ILogger<GenoaResourcepackCacheService> logger)
    {
        _staticData = staticData;
        _earthDbFactory = earthDbFactory;
        _logger = logger;
    }

    public async Task InitializeAsync(EventBusClient eventBusClient)
        => _subscriber = await eventBusClient.AddSubscriberAsync(
            "resourcepack",
            HandleEventAsync,
            exception =>
            {
                if (exception is not null)
                {
                    LogSubscriberError(_logger, exception);
                }

                return Task.CompletedTask;
            });

    private async Task HandleEventAsync(SubscriberEvent @event, CancellationToken cancellationToken)
    {
        if (@event.Type is "updated")
        {
            LogResourcePackUpdated(_logger);
            await InvalidateAsync(cancellationToken);
        }
    }

    public async Task InvalidateAsync(CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);

        try
        {
            _cachedZip?.Dispose();
            _cachedZip = null;
            _loaded = false;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<byte[]?> GetResourcePackFileAsync(string path, CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);

        try
        {
            var zip = await GetResourcePackLockedAsync(cancellationToken);
            if (zip is null)
            {
                return null;
            }

            var entry = zip.GetEntry(path);
            if (entry is null)
            {
                return null;
            }

            await using var entryStream = await entry.OpenAsync(FileAccess.Read, cancellationToken);

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
    {
        _cachedZip?.Dispose();
        _lock.Dispose();
    }

    private async Task<ZipArchive?> GetResourcePackLockedAsync(CancellationToken cancellationToken)
    {
        if (_loaded && DateTimeOffset.UtcNow < _expiresAt)
        {
            return _cachedZip;
        }

        _cachedZip?.Dispose();
        _cachedZip = await CreateResourcePackZipAsync(cancellationToken);
        _loaded = true;
        _expiresAt = DateTimeOffset.UtcNow.AddMinutes(15);

        return _cachedZip;
    }

    private async Task<ZipArchive?> CreateResourcePackZipAsync(CancellationToken cancellationToken)
    {
        var resourcePackPath = await ResolveResourcePackPathAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(resourcePackPath) || !File.Exists(resourcePackPath))
        {
            return null;
        }

        using var outer = await ZipFile.OpenReadAsync(resourcePackPath, cancellationToken);
        var innerEntry = outer.GetEntry("genoa.mcpack");
        if (innerEntry is null)
        {
            return null;
        }

        var ms = new MemoryStream();
        await using var innerStream = await innerEntry.OpenAsync(FileAccess.Read, cancellationToken);
        await innerStream.CopyToAsync(ms, cancellationToken);
        ms.Position = 0;

        return new ZipArchive(ms, ZipArchiveMode.Read);
    }

    private async Task<string?> ResolveResourcePackPathAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var earthDb = await _earthDbFactory.CreateDbContextAsync(cancellationToken);
            var activePack = await earthDb.SynthesizedResourcePacks
                .AsNoTracking()
                .OrderByDescending(p => p.BuildNumber)
                .FirstOrDefaultAsync(cancellationToken);

            if (activePack is not null)
            {
                var synthesizedPath = _staticData.Resourcepacks.GetCachedResourcepackPath(activePack.Id);
                if (File.Exists(synthesizedPath))
                {
                    return synthesizedPath;
                }
            }
        }
        catch (Exception exception)
        {
            LogResolveSynthesizedPackFailed(_logger, exception);
        }

        return _staticData.Resourcepacks.GenoaResourcepackPath;
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Error, Message = "Error in resourcepack event bus subscriber")]
    private static partial void LogSubscriberError(ILogger logger, Exception exception);

    [LoggerMessage(EventId = 2, Level = LogLevel.Information, Message = "Received resourcepack updated event. Invalidating resourcepack cache.")]
    private static partial void LogResourcePackUpdated(ILogger logger);

    [LoggerMessage(EventId = 3, Level = LogLevel.Warning, Message = "Failed to resolve synthesized resourcepack from database; falling back to base pack.")]
    private static partial void LogResolveSynthesizedPackFailed(ILogger logger, Exception exception);
}
