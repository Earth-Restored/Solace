using Serilog;
using Solace.Common;
using Solace.DB;
using Solace.EventBus.Client;
using Solace.ObjectStore.Client;

namespace Solace.ApiServer.Utils;

internal static class TileUtils
{
    private static EarthDB db => Program.DB;
    private static EventBusClient eventBus => Program.eventBus;
    private static readonly byte[] EmptyTilePng = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAIAAAACACAYAAADDPmHLAAABOklEQVR4nO3SMQ0AAAwCoNm/9HI83BLIOQmtnpnZB4CjEwABEgABEgABEgABEgABEgABEgABEgABEgABEgABEgABEgABEgABEgABEgABEgABEgABEgABEgABEgABEgABEgABEgABEgABEgABEgABEgABEgABEgABEgABEgABEgABEgABEgABEgABEgABEgABEgABEgABEgABEgABEgABEgABEgABEgABEgABEgABEgABEgABEgABEgABEgABEgABEgABEgABEgABEgABEgABEgABEgABEgABEgABEgABEgABEgABEgABEgABEgABEgABEgABEgABEgABEgABEgABEgABEgABEgABEgABEgABEgABEgABEgABEgABEgABEgABEgABEgABEgABEgABEgABEgABEgABEgABEgABEgABEgABEgABEgABEgABEgABEgABEgABEgABEgABEgABEgABEgABEgABEgABEgABEgABEgABEgABEgABEgABEgABEoAB1XQB3P+pKnEAAAAASUVORK5CYII=");

    private static RequestSender? _requestSender;
    private static readonly SemaphoreSlim _requestSenderLock = new(1, 1);

    public static async Task<bool> TryWriteTile(int tileX, int tileY, Stream dest, CancellationToken cancellationToken)
    {
        if (await TryWriteRenderedTile(tileX, tileY, dest, cancellationToken))
        {
            return true;
        }

        Log.Warning("Serving fallback tile {TileX},{TileY}", tileX, tileY);
        await dest.WriteAsync(EmptyTilePng, cancellationToken);
        return true;
    }

    private static async Task<bool> TryWriteRenderedTile(int tileX, int tileY, Stream dest, CancellationToken cancellationToken)
    {
        string? response;

        await _requestSenderLock.WaitAsync(cancellationToken);
        try
        {
            _requestSender ??= await eventBus.AddRequestSenderAsync();

            Task<string?> responseTask = _requestSender.RequestAsync("tile", "renderTile", Json.Serialize(new RenderTileRequest(tileX, tileY, 16)));
            Task timeoutTask = Task.Delay(TimeSpan.FromSeconds(8), cancellationToken);
            if (await Task.WhenAny(responseTask, timeoutTask) != responseTask)
            {
                Log.Warning("Tile render timed out for tile {TileX},{TileY}", tileX, tileY);
                await ResetRequestSenderAsync();
                return false;
            }

            response = await responseTask;
        }
        catch (Exception ex) when (ex is EventBusClientException or InvalidOperationException)
        {
            Log.Warning(ex, "Tile render request failed for tile {TileX},{TileY}", tileX, tileY);
            await ResetRequestSenderAsync();
            return false;
        }
        finally
        {
            _requestSenderLock.Release();
        }

        if (string.IsNullOrWhiteSpace(response))
        {
            Log.Warning("Tile renderer returned no data for tile {TileX},{TileY}", tileX, tileY);
            return false;
        }

        try
        {
            byte[] tilePng = Convert.FromBase64String(response);
            await dest.WriteAsync(tilePng, cancellationToken);
            return true;
        }
        catch (FormatException ex)
        {
            Log.Warning(ex, "Tile renderer returned invalid base64 for tile {TileX},{TileY}", tileX, tileY);
            return false;
        }
    }

    private static async Task ResetRequestSenderAsync()
    {
        if (_requestSender is not null)
        {
            try
            {
                await _requestSender.CloseAsync();
            }
            catch
            {
                // The connection is already broken; the next request will create a new sender.
            }
        }

        _requestSender = null;
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
