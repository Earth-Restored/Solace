using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Solace.Common.Utils;
using Solace.EventBus.Client;

namespace Solace.TappablesGenerator;

internal sealed partial class ActiveTiles : IAsyncDisposable
{
    private const int ACTIVE_TILE_RADIUS = 4;
    private static readonly TimeSpan ACTIVE_TILE_EXPIRY_TIME = TimeSpan.FromMinutes(2);

    private readonly ConcurrentDictionary<int, ActiveTile> _activeTiles = [];
    private readonly Spawner _spawner;
    private RequestHandler? _requestHandler;

    private readonly ILogger<ActiveTiles> _logger;

    public ActiveTiles(Spawner spawner, ILogger<ActiveTiles> logger)
    {
        _spawner = spawner;
        _logger = logger;
    }

    internal async Task InitializeAsync(EventBusClient eventBusClient)
        => _requestHandler = await eventBusClient.AddRequestHandlerAsync("tappables",
        async (request, cancellationToken) =>
        {
            if (request.Type is "activeTile")
            {
                ActiveTileNotification activeTileNotification;
                try
                {
                    activeTileNotification = JsonSerializer.Deserialize((string)request.Data.Value!, AppJsonContext.Default.ActiveTileNotification)!;
                }
                catch (Exception exception)
                {
                    LogCouldNotDeserialiseActiveTileNotificationEvent(exception);
                    return null;
                }

                var currentTime = DateTimeOffset.UtcNow;
                PruneActiveTiles(currentTime);

                var sideLength = (ACTIVE_TILE_RADIUS * 2) + 1;
                var newActiveTiles = new List<ActiveTile>(sideLength * sideLength);
                var allRequestedTiles = new List<(int X, int Y)>(sideLength * sideLength);

                for (var tileX = activeTileNotification.X - ACTIVE_TILE_RADIUS; tileX <= activeTileNotification.X + ACTIVE_TILE_RADIUS; tileX++)
                {
                    for (var tileY = activeTileNotification.Y - ACTIVE_TILE_RADIUS; tileY <= activeTileNotification.Y + ACTIVE_TILE_RADIUS; tileY++)
                    {
                        var activeTile = MarkTileActive(tileX, tileY, currentTime);
                        allRequestedTiles.Add((tileX, tileY));

                        if (activeTile.LatestActiveTime == activeTile.FirstActiveTime) // indicating that the tile is newly-active
                        {
                            newActiveTiles.Add(activeTile);
                        }
                    }
                }

                if (newActiveTiles.Count > 0)
                {
                    _spawner.SpawnTilesSync(newActiveTiles);
                }

                var (tappables, encounters) = _spawner.GetActiveLocationsForTiles(allRequestedTiles, currentTime);
                return JsonSerializer.Serialize(new ActiveTileResponse(tappables, encounters), AppJsonContext.Default.ActiveTileResponse);
            }
            else
            {
                return null;
            }
        },
        async exception =>
        {
            LogEventBusSubscriberError(exception);
            Console.Error.WriteLine(exception);
            Console.Error.Flush();
            Environment.Exit(333);
        });

    public IEnumerable<ActiveTile> GetActiveTiles(DateTimeOffset currentTime)
        => _activeTiles.Values.Where(activeTile => currentTime < activeTile.LatestActiveTime + ACTIVE_TILE_EXPIRY_TIME);

    public async ValueTask DisposeAsync()
    {
        if (_requestHandler is not null)
        {
            await _requestHandler.DisposeAsync();
        }
    }

    private ActiveTile MarkTileActive(int tileX, int tileY, DateTimeOffset currentTime)
    {
        var activeTile = _activeTiles.GetValueOrDefault(TileUtils.XYToInt(tileX, tileY));
        if (activeTile is null)
        {
            LogTileIsBecomingActive(tileX, tileY);
            activeTile = new ActiveTile(tileX, tileY, currentTime, currentTime);
        }
        else
        {
            activeTile = new ActiveTile(tileX, tileY, activeTile.FirstActiveTime, currentTime);
        }

        _activeTiles[TileUtils.XYToInt(tileX, tileY)] = activeTile;

        return activeTile;
    }

    private void PruneActiveTiles(DateTimeOffset currentTime)
    {
        List<KeyValuePair<int, ActiveTile>> entriesToRemove = [];

        foreach (var item in _activeTiles)
        {
            var activeTile = item.Value;
            if (activeTile.LatestActiveTime + ACTIVE_TILE_EXPIRY_TIME <= currentTime)
            {
                LogTileIsInactive(activeTile.TileX, activeTile.TileY);
                entriesToRemove.Add(item);
            }
        }

        foreach (var item in entriesToRemove)
        {
            _activeTiles.TryRemove(item.Key, out _);
        }

        if (entriesToRemove.Count > 0)
        {
            _spawner.RemoveInactiveTilesAsync(entriesToRemove.Select(item => item.Value))
                .Forget();
        }
    }

    internal sealed record ActiveTile(
        int TileX,
        int TileY,
        DateTimeOffset FirstActiveTime,
        DateTimeOffset LatestActiveTime
    );

    internal sealed record ActiveTileNotification(
        int X,
        int Y,
        string PlayerId
    );

    internal sealed record ActiveTileResponse(
        List<Tappable> Tappables,
        List<Encounter> Encounters
    );

    [LoggerMessage(Level = LogLevel.Error, Message = "Could not deserialise active tile notification event")]
    private partial void LogCouldNotDeserialiseActiveTileNotificationEvent(Exception exception);

    [LoggerMessage(Level = LogLevel.Error, Message = "Event bus subscriber error")]
    private partial void LogEventBusSubscriberError(Exception? exception);

    [LoggerMessage(Level = LogLevel.Information, Message = "Tile ({PosX}, {PosY}) is becoming active")]
    private partial void LogTileIsBecomingActive(int PosX, int PosY);

    [LoggerMessage(Level = LogLevel.Information, Message = "Tile ({PosX}, {PosY}) is inactive")]
    private partial void LogTileIsInactive(int PosX, int PosY);
}

