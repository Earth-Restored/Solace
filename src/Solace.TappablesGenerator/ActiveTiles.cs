using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Solace.Common;
using Solace.Common.Utils;
using Solace.EventBus.Client;

namespace Solace.TappablesGenerator;

internal sealed partial class ActiveTiles : IAsyncDisposable
{
    private const int ACTIVE_TILE_RADIUS = 3;
    private static readonly TimeSpan ACTIVE_TILE_EXPIRY_TIME = TimeSpan.FromMinutes(2);

    private readonly Dictionary<int, ActiveTile> _activeTiles = [];
    private IActiveTileListener? _activeTileListener;
    private RequestHandler? _requestHandler;

    private readonly ILogger<ActiveTiles> _logger;

    public ActiveTiles(ILogger<ActiveTiles> logger)
    {
        _logger = logger;
    }

    internal async Task InitializeAsync(EventBusClient eventBusClient, IActiveTileListener activeTileListener)
    {
        _activeTileListener = activeTileListener;

        _requestHandler = await eventBusClient.AddRequestHandlerAsync("tappables",
        async request =>
        {
            if (request.Type is "activeTile")
            {
                ActiveTileNotification activeTileNotification;
                try
                {
                    activeTileNotification = JsonSerializer.Deserialize(request.Data, AppJsonContext.Default.ActiveTileNotification)!;
                }
                catch (Exception exception)
                {
                    LogCouldNotDeserialiseActiveTileNotificationEvent(exception);
                    return null;
                }

                var currentTime = DateTimeOffset.UtcNow;
                PruneActiveTiles(currentTime);

                int sideLength = (ACTIVE_TILE_RADIUS * 2) + 1;
                var newActiveTiles = new List<ActiveTile>(sideLength * sideLength);
                for (int tileX = activeTileNotification.X - ACTIVE_TILE_RADIUS; tileX < activeTileNotification.X + ACTIVE_TILE_RADIUS + 1; tileX++)
                {
                    for (int tileY = activeTileNotification.Y - ACTIVE_TILE_RADIUS; tileY < activeTileNotification.Y + ACTIVE_TILE_RADIUS + 1; tileY++)
                    {
                        ActiveTile activeTile = MarkTileActive(tileX, tileY, currentTime);

                        if (activeTile.LatestActiveTime == activeTile.FirstActiveTime) // indicating that the tile is newly-active
                        {
                            newActiveTiles.Add(activeTile);
                        }
                    }
                }

                if (newActiveTiles.Count > 0)
                {
                    await activeTileListener.Active(newActiveTiles);
                }

                return string.Empty;
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
    }

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
        var activeTile = _activeTiles.GetValueOrDefault((tileX << 16) + tileY);
        if (activeTile is null)
        {
            LogTileIsBecomingActive(tileX, tileY);
            activeTile = new ActiveTile(tileX, tileY, currentTime, currentTime);
        }
        else
        {
            activeTile = new ActiveTile(tileX, tileY, activeTile.FirstActiveTime, currentTime);
        }

        _activeTiles[(tileX << 16) + tileY] = activeTile;

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
            _activeTiles.Remove(item.Key);
        }

        Debug.Assert(_activeTileListener is not null);

        _activeTileListener.Inactive(entriesToRemove.Select(item => item.Value));
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

    internal interface IActiveTileListener
    {
        Task Active(IEnumerable<ActiveTile> activeTiles);

        Task Inactive(IEnumerable<ActiveTile> activeTiles);
    }

    internal sealed class ActiveTileListener : IActiveTileListener
    {
        public Func<IEnumerable<ActiveTile>, Task>? OnActive;
        public Func<IEnumerable<ActiveTile>, Task>? OnInactive;

        public ActiveTileListener(Func<IEnumerable<ActiveTile>, Task>? active, Func<IEnumerable<ActiveTile>, Task>? inactive)
        {
            OnActive = active;
            OnInactive = inactive;
        }

        public Task Active(IEnumerable<ActiveTile> activeTiles)
            => OnActive?.Invoke(activeTiles) ?? Task.CompletedTask;

        public Task Inactive(IEnumerable<ActiveTile> activeTiles)
            => OnInactive?.Invoke(activeTiles) ?? Task.CompletedTask;
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Could not deserialise active tile notification event")]
    private partial void LogCouldNotDeserialiseActiveTileNotificationEvent(Exception exception);

    [LoggerMessage(Level = LogLevel.Error, Message = "Event bus subscriber error")]
    private partial void LogEventBusSubscriberError(Exception? exception);

    [LoggerMessage(Level = LogLevel.Information, Message = "Tile ({PosX}, {PosY}) is becoming active")]
    private partial void LogTileIsBecomingActive(int PosX, int PosY);

    [LoggerMessage(Level = LogLevel.Information, Message = "Tile ({PosX}, {PosY}) is inactive")]
    private partial void LogTileIsInactive(int PosX, int PosY);
}

