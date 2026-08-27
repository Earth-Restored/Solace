using Microsoft.EntityFrameworkCore;
using Serilog;
using Solace.Common;
using Solace.DB;
using Solace.EventBus.Client;
using Solace.ObjectStore.Client;

namespace Solace.ApiServer.Utils;

internal static class TileUtils
{
    public static async Task<bool> TryWriteTile(int tileX, int tileY, Stream dest, EarthDbContext earthDb, EventBusClient eventBus, ObjectStoreClient objectStore, CancellationToken cancellationToken)
    {
        ulong dbPos = ToDbPos(tileX, tileY);

        var tile = await earthDb.Tiles
            .AsNoTracking()
            .FirstOrDefaultAsync(tile => tile.Id == dbPos, cancellationToken: cancellationToken);

        if (tile is not null)
        {
            return await TryWriteTileFromObject(tile.ObjectStoreId, dest, objectStore, cancellationToken);
        }

        Log.Information("Rendering tile");
        await using var requestSender = await eventBus.AddRequestSenderAsync();
        string? tilePng64 = await requestSender.RequestAsync("tile", "renderTile", Json.Serialize(new RenderTileRequest(tileX, tileY, 16)));

        if (tilePng64 is null)
        {
            Log.Warning("Could not get tile (tile renderer did not respond to event bus request)");
            return false;
        }

        byte[] tilePng = Convert.FromBase64String(tilePng64);

        var tileObjectId = await objectStore.StoreAsync(tilePng);

        if (string.IsNullOrEmpty(tileObjectId))
        {
            Log.Warning("Failed to store tile to object store");
            return false;
        }

        tile = new DB.Models.Global.Tile()
        {
            Id = dbPos,
            ObjectStoreId = tileObjectId,
        };

        earthDb.Tiles.Add(tile);
        await earthDb.SaveChangesAsync(cancellationToken);

        Log.Debug($"Stored tile ({tileX}, {tileY}) to object store under id {tileObjectId}");

        await dest.WriteAsync(tilePng, cancellationToken);

        return true;
    }

    private static async Task<bool> TryWriteTileFromObject(string tileObjectId, Stream dest, ObjectStoreClient objectStoreClient, CancellationToken cancellationToken)
    {
        byte[]? tilePng = await objectStoreClient.GetAsync(tileObjectId);

        if (tilePng is null)
        {
            return false;
        }

        await dest.WriteAsync(tilePng, cancellationToken);

        return true;
    }

    private static ulong ToDbPos(int tileX, int tileY)
        => unchecked((ulong)((long)tileX | ((long)tileY << 32)));

    private sealed record RenderTileRequest(int TileX, int TileY, int Zoom);
}
