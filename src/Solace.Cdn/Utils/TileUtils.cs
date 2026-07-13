using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Solace.Common;
using Solace.DB;
using Solace.EventBus.Client;
using Solace.ObjectStore.Client;

namespace Solace.Cdn.Utils;

internal static partial class TileUtils
{
    private static readonly Func<EarthDbContext, ulong, CancellationToken, Task<DB.Models.Global.Tile?>> GetTileByIdAsync =
        EF.CompileAsyncQuery((EarthDbContext context, ulong id, CancellationToken ct) =>
            context.Tiles
                .AsNoTracking()
                .FirstOrDefault(tile => tile.Id == id));

    public static async Task<bool> TryWriteTile(int tileX, int tileY, int zoom, Stream dest, EarthDbContext earthDb, EventBusClient eventBus, ObjectStoreClient objectStore, ILogger logger, CancellationToken cancellationToken)
    {
        ulong dbPos = ToDbPos(tileX, tileY);

        var tile = await GetTileByIdAsync(earthDb, dbPos, cancellationToken);

        if (tile is not null)
        {
            return await TryWriteTileFromObject(tile.ObjectStoreId, dest, objectStore, cancellationToken);
        }

        LogRenderingTile(logger);
        await using var requestSender = await eventBus.AddRequestSenderAsync();
        string? tilePng64 = await requestSender.RequestAsync("tile", "renderTile", JsonSerializer.Serialize(new RenderTileRequest(tileX, tileY, zoom),AppJsonContext.Default.RenderTileRequest));

        if (string.IsNullOrEmpty(tilePng64))
        {
            LogTileRetreiveFail(logger);
            return false;
        }

        byte[] tilePng = Convert.FromBase64String(tilePng64);

        var tileObjectId = await objectStore.StoreAsync(tilePng, cancellationToken);

        if (string.IsNullOrEmpty(tileObjectId))
        {
            LogTileStoreFail(logger);
            return false;
        }

        tile = new DB.Models.Global.Tile()
        {
            Id = dbPos,
            ObjectStoreId = tileObjectId,
        };

        earthDb.Tiles.Add(tile);
        await earthDb.SaveChangesAsync(cancellationToken);

        LogTileStored(logger, tileX, tileY, tileObjectId);

        await dest.WriteAsync(tilePng, cancellationToken);

        return true;
    }

    private static async Task<bool> TryWriteTileFromObject(string tileObjectId, Stream destination, ObjectStoreClient objectStoreClient, CancellationToken cancellationToken)
    {
        using var tilePng = await objectStoreClient.GetStreamAsync(tileObjectId, cancellationToken);

        if (tilePng is null)
        {
            return false;
        }

        await tilePng.CopyToAsync(destination, cancellationToken);

        return true;
    }

    private static ulong ToDbPos(int tileX, int tileY)
        => unchecked((ulong)((long)tileX | ((long)tileY << 32)));

    internal sealed record RenderTileRequest(int TileX, int TileY, int Zoom);

    [LoggerMessage(Level = LogLevel.Information, Message = "Rendering tile")]
    private static partial void LogRenderingTile(ILogger logger);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not get tile (tile renderer did not respond to event bus request)")]
    private static partial void LogTileRetreiveFail(ILogger logger);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to store tile to object store")]
    private static partial void LogTileStoreFail(ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Stored tile ({TileX}, {TileY}) to object store under id {TileObjectId}")]
    private static partial void LogTileStored(ILogger logger, int TileX, int TileY, string TileObjectId);
}
