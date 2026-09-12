using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Solace.Common.Utils;
using Solace.EventBus.Client;

namespace Solace.TappablesGenerator;

internal sealed partial class Spawner : IHostedService, IAsyncDisposable
{
    private static readonly TimeSpan SPAWN_INTERVAL = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan GRACE_PERIOD = TimeSpan.FromSeconds(5);

    private readonly EventBusClient _eventBus;
    private readonly TappableGenerator _tappableGenerator;
    private readonly EncounterGenerator _encounterGenerator;
    private Publisher? _publisher;
    private CancellationTokenSource _cts = new();
    private volatile Task? _task;

    private readonly ILogger<Spawner> _logger;

    private readonly int _maxTappableLifetimeIntervals;

    private DateTimeOffset _spawnCycleTime;
    private int _spawnCycleIndex;
    private readonly ConcurrentDictionary<int, int> _lastSpawnCycleForTile = [];
    private readonly ConcurrentDictionary<int, ConcurrentDictionary<Guid, Tappable>> _tappables = [];
    private readonly ConcurrentDictionary<int, ConcurrentDictionary<Guid, Encounter>> _encounters = [];

    public Func<DateTimeOffset, IEnumerable<ActiveTiles.ActiveTile>>? GetActiveTiles { get; set; }

    public Spawner(EventBusClient eventBus, TappableGenerator tappableGenerator, EncounterGenerator encounterGenerator, ILogger<Spawner> logger)
    {
        _eventBus = eventBus;
        _tappableGenerator = tappableGenerator;
        _encounterGenerator = encounterGenerator;
        _logger = logger;

        _maxTappableLifetimeIntervals = (int)(long.Max((long)_tappableGenerator.GetMaxTappableLifetime().TotalMilliseconds, (long)_encounterGenerator.GetMaxEncounterLifetime().TotalMilliseconds) / (long)SPAWN_INTERVAL.TotalMilliseconds + 1);

        _spawnCycleTime = DateTimeOffset.UtcNow;
        _spawnCycleIndex = _maxTappableLifetimeIntervals;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (_publisher is null)
        {
            _publisher = await _eventBus.AddPublisherAsync();
        }

        if (_task is not null)
        {
            _cts.Cancel();

            while (_task is not null)
            {
                await Task.Delay(1, cancellationToken);
            }

            _cts = new CancellationTokenSource();
        }

#pragma warning disable CA2016 // Forward the 'CancellationToken' parameter to methods
        _task = Task.Run(async () =>
        {
            var cancellationToken = _cts.Token;

            var nextTime = DateTimeOffset.UtcNow + SPAWN_INTERVAL;

            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    await Task.Delay(int.Max(0, (int)(nextTime - DateTimeOffset.UtcNow).TotalMilliseconds), cancellationToken);

                    nextTime += SPAWN_INTERVAL;

                    await DoSpawnCycleAsync(cancellationToken);
                }
            }
            finally
            {
                _task = null;
            }
        });
#pragma warning restore CA2016 // Forward the 'CancellationToken' parameter to methods
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_task is not null)
        {
            _cts.Cancel();

            while (_task is not null)
            {
                await Task.Delay(1, cancellationToken);
            }

            _cts = new CancellationTokenSource();
        }
    }

    public void SpawnTilesSync(IEnumerable<ActiveTiles.ActiveTile> activeTiles)
    {
        var spawnCycleTime = _spawnCycleTime;
        var spawnCycleIndex = _spawnCycleIndex;

        while (spawnCycleTime < DateTimeOffset.UtcNow)
        {
            spawnCycleTime += SPAWN_INTERVAL;
            spawnCycleIndex++;
        }

        List<Tappable> tappables = [];
        List<Encounter> encounters = [];
        foreach (var activeTile in activeTiles)
        {
            DoSpawnCyclesForTile(activeTile.TileX, activeTile.TileY, spawnCycleTime, spawnCycleIndex, tappables, encounters);
        }

        var tappableCutoffTime = spawnCycleTime - SPAWN_INTERVAL;
        tappables.RemoveAll(tappable => tappable.SpawnTime + tappable.ValidFor < tappableCutoffTime);
        encounters.RemoveAll(encounter => encounter.SpawnTime + encounter.ValidFor < tappableCutoffTime);

        Prune(spawnCycleTime);
    }

    public (List<Tappable> Tappables, List<Encounter> Encounters) GetActiveLocationsForTiles(IEnumerable<(int X, int Y)> tiles, DateTimeOffset currentTime)
    {
        List<Tappable> tappables = [];
        List<Encounter> encounters = [];

        foreach (var (X, Y) in tiles)
        {
            if (_tappables.TryGetValue(TileUtils.XYToInt(X, Y), out var tileTappables))
            {
                foreach (var tappable in tileTappables.Values)
                {
                    if (tappable.SpawnTime + tappable.ValidFor > currentTime)
                    {
                        tappables.Add(tappable);
                    }
                }
            }

            if (_encounters.TryGetValue(TileUtils.XYToInt(X, Y), out var tileEncounters))
            {
                foreach (var encounter in tileEncounters.Values)
                {
                    if (encounter.SpawnTime + encounter.ValidFor > currentTime)
                    {
                        encounters.Add(encounter);
                    }
                }
            }
        }

        return (tappables, encounters);
    }

    public Task RemoveInactiveTilesAsync(IEnumerable<ActiveTiles.ActiveTile> inactiveTiles)
    {
        foreach (var activeTile in inactiveTiles)
        {
            var key = TileUtils.XYToInt(activeTile.TileX, activeTile.TileY);
            _lastSpawnCycleForTile.TryRemove(key, out _);
            _tappables.TryRemove(key, out _);
            _encounters.TryRemove(key, out _);
        }

        return Task.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        if (_publisher is not null)
        {
            await _publisher.DisposeAsync();
        }
    }

    private async Task DoSpawnCycleAsync(CancellationToken cancellationToken = default)
    {
        var activeTiles = GetActiveTiles?.Invoke(_spawnCycleTime) ?? [];

        while (_spawnCycleTime < DateTimeOffset.UtcNow)
        {
            _spawnCycleTime += SPAWN_INTERVAL;
            _spawnCycleIndex++;
        }

        List<Tappable> tappables = [];
        List<Encounter> encounters = [];
        foreach (var activeTile in activeTiles)
        {
            DoSpawnCyclesForTile(activeTile.TileX, activeTile.TileY, _spawnCycleTime, _spawnCycleIndex, tappables, encounters);
        }

        var tappableCutoffTime = _spawnCycleTime - SPAWN_INTERVAL;

        tappables.RemoveAll(tappable => tappable.SpawnTime + tappable.ValidFor < tappableCutoffTime);
        encounters.RemoveAll(encounter => encounter.SpawnTime + encounter.ValidFor < tappableCutoffTime);

        Prune(_spawnCycleTime);

        await SendSpawnedTappablesAsync(tappables, encounters, cancellationToken);
    }

    private void DoSpawnCyclesForTile(int tileX, int tileY, DateTimeOffset spawnCycleTime, int spawnCycleIndex, List<Tappable> tappables, List<Encounter> encounters)
    {
        var key = TileUtils.XYToInt(tileX, tileY);
        var lastSpawnCycle = _lastSpawnCycleForTile.GetValueOrDefault(key);
        var isNewTile = lastSpawnCycle == 0;
        var cyclesToSpawn = int.Min(spawnCycleIndex - lastSpawnCycle, _maxTappableLifetimeIntervals);
        for (var index = 0; index < cyclesToSpawn; index++)
        {
            var isLastCycle = index == cyclesToSpawn - 1;
            if (isNewTile && isLastCycle)
            {
                SpawnTappablesForTile(tileX, tileY, DateTimeOffset.UtcNow, tappables, encounters, immediateSpawn: true);
            }
            else
            {
                SpawnTappablesForTile(tileX, tileY, spawnCycleTime - SPAWN_INTERVAL * (cyclesToSpawn - index - 1), tappables, encounters);
            }
        }

        _lastSpawnCycleForTile[key] = spawnCycleIndex;
    }

    private void SpawnTappablesForTile(int tileX, int tileY, DateTimeOffset currentTime, List<Tappable> tappables, List<Encounter> encounters, bool immediateSpawn = false)
    {
        var tileKey = TileUtils.XYToInt(tileX, tileY);
        var spawnedTappables = _tappableGenerator.GenerateTappables(tileX, tileY, currentTime, immediateSpawn);
        foreach (var tappable in spawnedTappables)
        {
            _tappables.GetOrAdd(tileKey, static _ => [])[tappable.Id] = tappable;
            tappables.Add(tappable);
        }

        var spawnedEncounters = _encounterGenerator.GenerateEncounters(tileX, tileY, currentTime, immediateSpawn);
        foreach (var encounter in spawnedEncounters)
        {
            _encounters.GetOrAdd(tileKey, static _ => [])[encounter.Id] = encounter;
            encounters.Add(encounter);
        }
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

        _tappables.RemoveAll(entry => entry.Value.IsEmpty);

        foreach (var tileEncounters in _encounters.Values)
        {
            tileEncounters.RemoveAll(entry =>
            {
                var encounter = entry.Value;
                var expiresAt = encounter.SpawnTime + encounter.ValidFor;
                return expiresAt + GRACE_PERIOD <= currentTime;
            });
        }

        _encounters.RemoveAll(entry => entry.Value.IsEmpty);
    }

    private async Task SendSpawnedTappablesAsync(List<Tappable> tappables, List<Encounter> encounters, CancellationToken cancellationToken = default)
    {
        Debug.Assert(_publisher is not null);

        if (!await _publisher.PublishAsync("tappables", "tappableSpawn", JsonSerializer.Serialize(tappables, AppJsonContext.Default.ListTappable), cancellationToken))
        {
            LogEventBusServerRejectedTappableSpawnEvent();
        }

        if (!await _publisher.PublishAsync("tappables", "encounterSpawn", JsonSerializer.Serialize(encounters, AppJsonContext.Default.ListEncounter), cancellationToken))
        {
            LogEventBusServerRejectedEncounterSpawnEvent();
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Event bus server rejected tappable spawn event")]
    private partial void LogEventBusServerRejectedTappableSpawnEvent();

    [LoggerMessage(Level = LogLevel.Error, Message = "Event bus server rejected encounter spawn event")]
    private partial void LogEventBusServerRejectedEncounterSpawnEvent();
}
