using System.Diagnostics;
using System.Text.Json.Serialization;
using Solace.Common;
using Solace.Common.Utils;
using Solace.EventBus.Client;

namespace Solace.ApiServer.Utils;

internal sealed partial class TappablesManager : IAsyncDisposable
{
    private static readonly TimeSpan GRACE_PERIOD = TimeSpan.FromSeconds(5);

    private Subscriber? _subscriber;
    private RequestSender? _requestSender;

    private readonly ILogger _logger;

    private readonly Dictionary<string, Dictionary<Guid, Tappable>> _tappables = [];
    private readonly Dictionary<string, Dictionary<Guid, Encounter>> _encounters = [];
    private int _pruneCounter;

    public TappablesManager(ILogger<TappablesManager> logger)
    {
        _logger = logger;
    }

    internal async Task InitializeAsync(EventBusClient eventBusClient)
    {
        _subscriber = await eventBusClient.AddSubscriberAsync("tappables",
            HandleEvent,
            async exception =>
            {
                LogTappablesEventBusSubscriberError(exception);
                Console.Error.WriteLine(exception);
                Console.Error.Flush();
                Environment.Exit(1);
            });
        _requestSender = await eventBusClient.AddRequestSenderAsync();
    }

    public Tappable[] GetTappablesAround(double lat, double lon, double radius)
        => [.. GetTileIdsAround(lat, lon, radius)
            .Select(tileId => _tappables.GetValueOrDefault(tileId))
            .Where(tappables => tappables is not null)
            .Select(items => items!.Values)
            .SelectMany(stream => stream)
            .Where(tappable =>
            {
                double dx = LonToX(tappable.Lon) * (1 << 16) - LonToX(lon) * (1 << 16);
                double dy = LatToY(tappable.Lat) * (1 << 16) - LatToY(lat) * (1 << 16);
                double distanceSquared = dx * dx + dy * dy;
                return distanceSquared <= radius * radius;
            })];

    public Encounter[] GetEncountersAround(double lat, double lon, double radius)
        => [.. GetTileIdsAround(lat, lon, radius)
            .Select(tileId => _encounters.GetValueOrDefault(tileId))
            .Where(encounters => encounters is not null)
            .SelectMany(encounters => encounters!.Values)
            .Where(encounter =>
            {
                double dx = LonToX(encounter.Lon) * (1 << 16) - LonToX(lon) * (1 << 16);
                double dy = LatToY(encounter.Lat) * (1 << 16) - LatToY(lat) * (1 << 16);
                double distanceSquared = dx * dx + dy * dy;
                return distanceSquared <= radius * radius;
            })];

    public Encounter[] GetEncountersAround(float lat, float lon, float radius)
        => [.. GetTileIdsAround(lat, lon, radius)
            .Select(tileId => _encounters.GetValueOrDefault(tileId))
            .Where(encounters => encounters is not null)
            .Select(encounters => encounters!.Values)
            .SelectMany(encounters => encounters)
            .Where(encounter =>
            {
                double dx = LonToX(encounter.Lon) * (1 << 16) - LonToX(lon) * (1 << 16);
                double dy = LatToY(encounter.Lat) * (1 << 16) - LatToY(lat) * (1 << 16);
                double distanceSquared = dx * dx + dy * dy;
                return distanceSquared <= radius * radius;
            })];

    private static string[] GetTileIdsAround(double lat, double lon, double radius)
    {
        int tileX = XToTile(LonToX(lon));
        int tileY = YToTile(LatToY(lat));
        int tileRadius = (int)Math.Ceiling(radius);
        int sideLength = (tileRadius * 2) + 1;

        return [.. Enumerable.Range(tileX - tileRadius, sideLength).Select(x => Enumerable.Range(tileY - tileRadius, sideLength).Select(y => $"{x}_{y}")).SelectMany(stream => stream)];
    }

    public Tappable? GetTappableWithId(Guid id, string tileId)
    {
        var tappablesInTile = _tappables.GetValueOrDefault(tileId);
        if (tappablesInTile is not null)
        {
            var tappable = tappablesInTile.GetValueOrDefault(id);
            if (tappable is not null)
            {
                return tappable;
            }
        }

        return null;
    }

    public Encounter? GetEncounterWithId(Guid id, string tileId)
    {
        var encountersInTile = _encounters.GetValueOrDefault(tileId);
        if (encountersInTile is not null)
        {
            var encounter = encountersInTile.GetValueOrDefault(id);
            if (encounter is not null)
            {
                return encounter;
            }
        }

        return null;
    }

#pragma warning disable IDE0060 // Remove unused parameter
    public static bool IsTappableValidFor(Tappable tappable, DateTimeOffset requestTime, float lat, float lon)
#pragma warning restore IDE0060 // Remove unused parameter
    {
        if (tappable.SpawnTime - GRACE_PERIOD > requestTime || tappable.SpawnTime + tappable.ValidFor + GRACE_PERIOD <= requestTime)
        {
            return false;
        }

        // TODO: check player location is in radius, account for boosts

        return true;
    }

    // TODO: actually use this
#pragma warning disable IDE0060 // Remove unused parameter
#pragma warning disable CA1822 // Mark members as static
    public bool IsEncounterValidFor(Encounter encounter, DateTimeOffset requestTime, float lat, float lon)
#pragma warning restore CA1822 // Mark members as static
#pragma warning restore IDE0060 // Remove unused parameter
    {
        if (encounter.SpawnTime - GRACE_PERIOD > requestTime || encounter.SpawnTime + encounter.ValidFor <= requestTime) // no grace period when checking end time because the buildplate instance shutdown does not include the grace period anyway
        {
            return false;
        }

        // TODO: check player location is in radius, account for boosts

        return true;
    }

    public async Task NotifyTileActiveAsync(Guid accountId, double lat, double lon)
    {
        Debug.Assert(_requestSender is not null);

        var tileX = XToTile(LonToX(lon));
        var tileY = YToTile(LatToY(lat));
        var response = await _requestSender.RequestAsync("tappables", "activeTile", Json.Serialize(new ActiveTileNotification(tileX, tileY, accountId.ToString())));
        if (response is null)
        {
            LogActiveTileNotificationEventWasRejectedIgnored();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_subscriber is not null)
        {
            await _subscriber.DisposeAsync();
        }

        if (_requestSender is not null)
        {
            await _requestSender.DisposeAsync();
        }
    }

    private sealed record ActiveTileNotification(
        int X,
        int Y,
        string PlayerId
    );

    private Task HandleEvent(SubscriberEvent @event)
    {
        switch (@event.Type)
        {
            case "tappableSpawn":
                {
                    Tappable[]? tappables;
                    try
                    {
                        tappables = Json.Deserialize<Tappable[]>(@event.Data);
                    }
                    catch (Exception exception)
                    {
                        LogFailedToDeserialiseTappableSpawnEvent(exception);
                        break;
                    }

                    Debug.Assert(tappables is not null);

                    foreach (var tappable in tappables)
                    {
                        AddTappable(tappable);
                    }

                    if (_pruneCounter++ == 10)
                    {
                        _pruneCounter = 0;
                        Prune(@event.Timestamp);
                    }
                }

                break;
            case "encounterSpawn":
                {
                    Encounter[]? encounters;

                    try
                    {
                        encounters = Json.Deserialize<Encounter[]>(@event.Data);
                    }
                    catch (Exception exception)
                    {
                        LogFailedToDeserialiseEncounterSpawnEvent(exception);
                        break;
                    }

                    Debug.Assert(encounters is not null);

                    foreach (var encounter in encounters)
                    {
                        AddEncounter(encounter);
                    }

                    if (_pruneCounter++ == 10)
                    {
                        _pruneCounter = 0;
                        Prune(@event.Timestamp);
                    }
                }

                break;
        }

        return Task.CompletedTask;
    }

    private void AddTappable(Tappable tappable)
    {
        var tileId = LocationToTileId(tappable.Lat, tappable.Lon);
        _tappables.ComputeIfAbsent(tileId, tileId1 => [])![tappable.Id] = tappable;
    }

    private void AddEncounter(Encounter encounter)
    {
        var tileId = LocationToTileId(encounter.Lat, encounter.Lon);
        _encounters.ComputeIfAbsent(tileId, tileId1 => [])![encounter.Id] = encounter;
    }

    private void Prune(DateTimeOffset currentTime)
    {
        foreach (var tileTappables in _tappables.Values)
        {
            tileTappables.RemoveAll(entry =>
            {
                var tappable = entry.Value;
                var expiresAt = tappable.SpawnTime + tappable.ValidFor;
                return expiresAt + GRACE_PERIOD <= currentTime;
            });
        }

        _tappables.RemoveAll(entry => entry.Value.Count is 0);

        foreach (var tileEncounters in _encounters.Values)
        {
            tileEncounters.RemoveAll(entry =>
            {
                var encounter = entry.Value;
                var expiresAt = encounter.SpawnTime + encounter.ValidFor;
                return expiresAt + GRACE_PERIOD <= currentTime;
            });
        }

        _encounters.RemoveAll(entry => entry.Value.Count is 0);
    }

    public static string LocationToTileId(float lat, float lon)
        => $"{XToTile(LonToX(lon))}_{YToTile(LatToY(lat))}";

    private static double LonToX(double lon)
        => (1.0 + double.DegreesToRadians(lon) / double.Pi) / 2.0;

    private static double LatToY(double lat)
        => (1.0 - double.Log(double.Tan(double.DegreesToRadians(lat)) + 1.0 / double.Cos(double.DegreesToRadians(lat))) / double.Pi) / 2.0;

    private static int XToTile(double x)
        => (int)double.Floor(x * (1 << 16));

    private static int YToTile(double y)
        => (int)double.Floor(y * (1 << 16));

    internal sealed record Tappable(
        Guid Id,
        float Lat,
        float Lon,
        DateTimeOffset SpawnTime,
        TimeSpan ValidFor,
        string Icon,
        Tappable.RarityE Rarity,
        Tappable.Item[] Items
    )
    {
        [JsonConverter(typeof(JsonStringEnumConverter<RarityE>))]
        internal enum RarityE
        {
            COMMON,
            UNCOMMON,
            RARE,
            EPIC,
            LEGENDARY,
        }

        internal sealed record Item(
            Guid Id,
            int Count
        );
    }

    internal sealed record Encounter(
        Guid Id,
        float Lat,
        float Lon,
        DateTimeOffset SpawnTime,
        TimeSpan ValidFor,
        string Icon,
        Encounter.RarityE Rarity,
        Guid EncounterBuildplateId
    )
    {
        [JsonConverter(typeof(JsonStringEnumConverter<RarityE>))]
        internal enum RarityE
        {
            COMMON,
            UNCOMMON,
            RARE,
            EPIC,
            LEGENDARY,
        }
    }

    [LoggerMessage(Level = LogLevel.Critical, Message = "Tappables event bus subscriber error")]
    private partial void LogTappablesEventBusSubscriberError(Exception? exception);

    [LoggerMessage(Level = LogLevel.Error, Message = "Active tile notification event was rejected/ignored")]
    private partial void LogActiveTileNotificationEventWasRejectedIgnored();

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to deserialise tappable spawn event")]
    private partial void LogFailedToDeserialiseTappableSpawnEvent(Exception exception);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to deserialise encounter spawn event")]
    private partial void LogFailedToDeserialiseEncounterSpawnEvent(Exception exception);
}
