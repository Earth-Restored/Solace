using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Solace.Common.Utils;
using Solace.StaticData;

namespace Solace.TappablesGenerator;

internal sealed partial class EncounterGenerator
{
    // TODO: make these configurable
    private const int CHANCE_PER_TILE = 4;
    private static readonly TimeSpan MIN_DELAY = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan MAX_DELAY = TimeSpan.FromMinutes(2);

    private readonly StaticData.StaticDataProvider _staticData;
    private readonly TimeSpan _maxDuration;

    private readonly Random _random;

    public EncounterGenerator(StaticData.StaticDataProvider staticData, ILogger<EncounterGenerator> logger)
    {
        _staticData = staticData;

        if (_staticData.EncountersConfig.Encounters.Length == 0)
        {
            LogNoEncounterConfigsProvided(logger);
        }

        _maxDuration = TimeSpan.FromSeconds(_staticData.EncountersConfig.Encounters.Select(encounterConfig => encounterConfig.Duration).DefaultIfEmpty().Max());

        _random = new Random();
    }

    public TimeSpan GetMaxEncounterLifetime()
        => MAX_DELAY + _maxDuration + TimeSpan.FromSeconds(30 * 1000);

    public IEnumerable<Encounter> GenerateEncounters(int tileX, int tileY, DateTimeOffset currentTime)
    {
        if (_staticData.EncountersConfig.Encounters is [])
        {
            return [];
        }

        List<Encounter> encounters = [];
#pragma warning disable CA5394 // Do not use insecure randomness - idc
        if (_random.Next(0, CHANCE_PER_TILE) == 0)
        {
            var spawnDelay = TimeSpan.FromTicks(_random.NextInt64(MIN_DELAY.Ticks, MAX_DELAY.Ticks + 1));

            var encounterConfig = _staticData.EncountersConfig.Encounters[_random.Next(0, _staticData.EncountersConfig.Encounters.Length)];
#pragma warning restore CA5394 // Do not use insecure randomness

            Span<float> tileBounds = stackalloc float[4];
            GetTileBounds(tileX, tileY, tileBounds);
            var lat = _random.NextSingle(tileBounds[1], tileBounds[0]);
            var lon = _random.NextSingle(tileBounds[2], tileBounds[3]);

            var encounter = new Encounter(
                Guid.CreateVersion7(),
                lat,
                lon,
                currentTime + spawnDelay,
                TimeSpan.FromSeconds(encounterConfig.Duration),
                encounterConfig.Icon,
                Encounter.RarityE.FromStaticData(encounterConfig.Rarity),
                encounterConfig.EncounterBuildplateId
            );

            encounters.Add(encounter);
        }

        return encounters;
    }

    private static void GetTileBounds(int tileX, int tileY, Span<float> dest)
    {
        Debug.Assert(dest.Length >= 4);

        dest[0] = YToLat((float)tileY / (1 << 16));
        dest[1] = YToLat((float)(tileY + 1) / (1 << 16));
        dest[2] = XToLon((float)tileX / (1 << 16));
        dest[3] = XToLon((float)(tileX + 1) / (1 << 16));
    }

    private static float XToLon(float x)
        => ((x * 2.0f - 1.0f) * float.Pi) * (180f / float.Pi);

    private static float YToLat(float y)
        => (float.Atan(float.Sinh((1.0f - y * 2.0f) * float.Pi))) * (180f / float.Pi);

    [LoggerMessage(Level = LogLevel.Warning, Message = "No encounter configs provided")]
    private static partial void LogNoEncounterConfigsProvided(ILogger logger);
}