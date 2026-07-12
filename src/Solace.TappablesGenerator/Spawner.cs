using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Solace.Common;
using Solace.Common.Utils;
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
    private readonly Dictionary<int, int> _lastSpawnCycleForTile = [];

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
            await Task.Delay(Math.Max(0, (int)(nextTime - DateTimeOffset.UtcNow).TotalMilliseconds));

            nextTime += SPAWN_INTERVAL;

            await DoSpawnCycle();
        }
    }

    [Obsolete($"Use {nameof(SpawnTiles)} instead.")]
    public async Task SpawnTile(int tileX, int tileY)
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

        await SendSpawnedTappables(tappables, encounters);
    }

    public async Task SpawnTiles(IEnumerable<ActiveTiles.ActiveTile> activeTiles)
    {
        var spawnCycleTime = _spawnCycleTime;
        int spawnCycleIndex = _spawnCycleIndex;

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

        await SendSpawnedTappables(tappables, encounters);
    }

    public async ValueTask DisposeAsync()
    {
        if (_publisher is not null)
        {
            await _publisher.DisposeAsync();
        }
    }

    private async Task DoSpawnCycle()
    {
        var activeTiles = _activeTiles.GetActiveTiles(_spawnCycleTime);

        while (_spawnCycleTime < DateTimeOffset.UtcNow)
        {
            _spawnCycleTime += SPAWN_INTERVAL;
            _spawnCycleIndex++;
        }

        List<Tappable> tappables = [];
        List<Encounter> encounters = [];
        foreach (ActiveTiles.ActiveTile activeTile in activeTiles)
        {
            DoSpawnCyclesForTile(activeTile.TileX, activeTile.TileY, _spawnCycleTime, _spawnCycleIndex, tappables, encounters);
        }

        var tappableCutoffTime = _spawnCycleTime - SPAWN_INTERVAL;

        tappables.RemoveAll(tappable => tappable.SpawnTime + tappable.ValidFor < tappableCutoffTime);
        encounters.RemoveAll(encounter => encounter.SpawnTime + encounter.ValidFor < tappableCutoffTime);

        await SendSpawnedTappables(tappables, encounters);
    }

    private void DoSpawnCyclesForTile(int tileX, int tileY, DateTimeOffset spawnCycleTime, int spawnCycleIndex, List<Tappable> tappables, List<Encounter> encounters)
    {
        int lastSpawnCycle = _lastSpawnCycleForTile.GetValueOrDefault((tileX << 16) + tileY);
        int cyclesToSpawn = Math.Min(spawnCycleIndex - lastSpawnCycle, _maxTappableLifetimeIntervals);
        for (int index = 0; index < cyclesToSpawn; index++)
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

    private async Task SendSpawnedTappables(List<Tappable> tappables, List<Encounter> encounters)
    {
        Debug.Assert(_publisher is not null);

        if (!await _publisher.PublishAsync("tappables", "tappableSpawn", JsonSerializer.Serialize(tappables, AppJsonContext.Default.ListTappable)))
        {
            LogEventBusServerRejectedTappableSpawnEvent();
        }

        if (!await _publisher.PublishAsync("tappables", "encounterSpawn", JsonSerializer.Serialize(encounters, AppJsonContext.Default.ListEncounter)))
        {
            LogEventBusServerRejectedEncounterSpawnEvent();
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Event bus server rejected tappable spawn event")]
    private partial void LogEventBusServerRejectedTappableSpawnEvent();

    [LoggerMessage(Level = LogLevel.Error, Message = "Event bus server rejected encounter spawn event")]
    private partial void LogEventBusServerRejectedEncounterSpawnEvent();
}
