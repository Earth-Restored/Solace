using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Solace.EventBus.Client;

namespace Solace.TappablesGenerator;

internal sealed partial class Spawner : IAsyncDisposable
{
    private static readonly TimeSpan SPAWN_INTERVAL = TimeSpan.FromSeconds(15);

    private readonly ActiveTiles _activeTiles;
    private readonly TappableGenerator _tappableGenerator;
    private readonly EncounterGenerator _encounterGenerator;
    private Publisher? _publisher;

    private readonly ILogger<Spawner> _logger;

    private readonly int _maxTappableLifetimeIntervals;

    private DateTimeOffset _spawnCycleTime;
    private int _spawnCycleIndex;
    private readonly ConcurrentDictionary<int, int> _lastSpawnCycleForTile = [];

    public Spawner(ActiveTiles activeTiles, TappableGenerator tappableGenerator, EncounterGenerator encounterGenerator, ILogger<Spawner> logger)
    {
        _activeTiles = activeTiles;

        _tappableGenerator = tappableGenerator;
        _encounterGenerator = encounterGenerator;

        _logger = logger;

        _maxTappableLifetimeIntervals = (int)(long.Max((long)_tappableGenerator.GetMaxTappableLifetime().TotalMilliseconds, (long)_encounterGenerator.GetMaxEncounterLifetime().TotalMilliseconds) / (long)SPAWN_INTERVAL.TotalMilliseconds + 1);

        _spawnCycleTime = DateTimeOffset.UtcNow;
        _spawnCycleIndex = _maxTappableLifetimeIntervals;
    }

    internal async Task InitializeAsync(EventBusClient eventBusClient)
        => _publisher = await eventBusClient.AddPublisherAsync();

    public async Task RunAsync()
    {
        var nextTime = DateTimeOffset.UtcNow + SPAWN_INTERVAL;
        while (true)
        {
            await Task.Delay(int.Max(0, (int)(nextTime - DateTimeOffset.UtcNow).TotalMilliseconds));

            nextTime += SPAWN_INTERVAL;

            await DoSpawnCycleAsync();
        }
    }

    [Obsolete($"Use {nameof(SpawnTilesAsync)} instead.")]
    public async Task SpawnTileAsync(int tileX, int tileY, CancellationToken cancellationToken = default)
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
        DoSpawnCyclesForTile(tileX, tileY, spawnCycleTime, spawnCycleIndex, tappables, encounters);

        var tappableCutoffTime = spawnCycleTime - SPAWN_INTERVAL;
        tappables.RemoveAll(tappable => tappable.SpawnTime + tappable.ValidFor < tappableCutoffTime);
        encounters.RemoveAll(encounter => encounter.SpawnTime + encounter.ValidFor < tappableCutoffTime);

        await SendSpawnedTappablesAsync(tappables, encounters, cancellationToken);
    }

    public async Task SpawnTilesAsync(IEnumerable<ActiveTiles.ActiveTile> activeTiles, CancellationToken cancellationToken = default)
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

        await SendSpawnedTappablesAsync(tappables, encounters, cancellationToken);
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
        var activeTiles = _activeTiles.GetActiveTiles(_spawnCycleTime);

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

        await SendSpawnedTappablesAsync(tappables, encounters, cancellationToken);
    }

    private void DoSpawnCyclesForTile(int tileX, int tileY, DateTimeOffset spawnCycleTime, int spawnCycleIndex, List<Tappable> tappables, List<Encounter> encounters)
    {
        var lastSpawnCycle = _lastSpawnCycleForTile.GetValueOrDefault((tileX << 16) + tileY);
        var cyclesToSpawn = int.Min(spawnCycleIndex - lastSpawnCycle, _maxTappableLifetimeIntervals);
        for (var index = 0; index < cyclesToSpawn; index++)
        {
            SpawnTappablesForTile(tileX, tileY, spawnCycleTime - SPAWN_INTERVAL * (cyclesToSpawn - index - 1), tappables, encounters);
        }

        _lastSpawnCycleForTile[(tileX << 16) + tileY] = spawnCycleIndex;
    }

    private void SpawnTappablesForTile(int tileX, int tileY, DateTimeOffset currentTime, List<Tappable> tappables, List<Encounter> encounters)
    {
        tappables.AddRange(_tappableGenerator.GenerateTappables(tileX, tileY, currentTime));
        encounters.AddRange(_encounterGenerator.GenerateEncounters(tileX, tileY, currentTime));
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
