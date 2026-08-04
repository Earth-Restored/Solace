using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Solace.Db.Earth;
using Solace.EventBus.Client;
using Solace.ObjectStore.Client;

namespace Solace.Cdn.Utils;

internal static partial class TileUtils
{
    public static async Task<bool> TryWriteTile(int tileX, int tileY, int zoom, Stream dest, IDbContextFactory<EarthDbContext> earthDbFactory, EventBusClient eventBus, ObjectStoreClient objectStore, ILogger logger, CancellationToken cancellationToken)
    {
        var dbPos = ToDbPos(tileX, tileY);

        Guid? objectStoreId = null;

        await using (var earthDb = await earthDbFactory.CreateDbContextAsync(cancellationToken))
        {
            var tile = await earthDb.Tiles
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == dbPos, cancellationToken);

            objectStoreId = tile?.ObjectStoreId;
        }

        if (objectStoreId is not null)
        {
            return await TryWriteTileFromObject(objectStoreId.Value, dest, objectStore, cancellationToken);
        }

        LogRenderingTile(logger);
        await using var requestSender = await eventBus.AddRequestSenderAsync();
        var tilePng64 = (await requestSender.RequestAsync("tile", "renderTile", JsonSerializer.Serialize(new RenderTileRequest(tileX, tileY, zoom), AppJsonContext.Default.RenderTileRequest), cancellationToken))?.Value as string;

        if (string.IsNullOrEmpty(tilePng64))
        {
            LogTileRetreiveFail(logger);
            return false;
        }

        var tilePng = Convert.FromBase64String(tilePng64);

        var tileObjectId = await objectStore.StoreAsync(tilePng, cancellationToken);

        if (tileObjectId is null)
        {
            LogTileStoreFail(logger);
            return false;
        }

        await using (var earthDb = await earthDbFactory.CreateDbContextAsync(cancellationToken))
        {
            var newTile = new Db.Earth.Models.Global.Tile()
            {
                Id = dbPos,
                ObjectStoreId = tileObjectId.Value,
            };

            earthDb.Tiles.Add(newTile);
            await earthDb.SaveChangesAsync(cancellationToken);
        }

        LogTileStored(logger, tileX, tileY, tileObjectId.Value);

        await dest.WriteAsync(tilePng, cancellationToken);

        return true;
    }

    private static async Task<bool> TryWriteTileFromObject(Guid tileObjectId, Stream destination, ObjectStoreClient objectStoreClient, CancellationToken cancellationToken)
    {
        using var tilePng = await objectStoreClient.GetStreamAsync(tileObjectId, cancellationToken);

        if (tilePng is null)
        {
            return false;
        }

        await tilePng.CopyToAsync(destination, cancellationToken);

        return true;
    }

    private static long ToDbPos(int tileX, int tileY)
        => unchecked((long)tileX | ((long)tileY << 32));

    internal sealed record RenderTileRequest(int TileX, int TileY, int Zoom);

    [LoggerMessage(Level = LogLevel.Information, Message = "Rendering tile")]
    private static partial void LogRenderingTile(ILogger logger);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not get tile (tile renderer did not respond to event bus request)")]
    private static partial void LogTileRetreiveFail(ILogger logger);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to store tile to object store")]
    private static partial void LogTileStoreFail(ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Stored tile ({TileX}, {TileY}) to object store under id {TileObjectId}")]
    private static partial void LogTileStored(ILogger logger, int TileX, int TileY, Guid TileObjectId);
}
