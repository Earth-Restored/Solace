using Microsoft.Extensions.Logging;
using Solace.Common;
using Solace.Common.Utils;
using Solace.StaticData;

namespace Solace.TappablesGenerator;

internal sealed partial class TappableGenerator
{
    // TODO: make these configurable
    private const int MIN_COUNT = 1;
    private const int MAX_COUNT = 3;
    private static readonly TimeSpan MIN_DURATION = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan MAX_DURATION = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan MIN_DELAY = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan MAX_DELAY = TimeSpan.FromMinutes(2);

    private readonly StaticData.StaticData _staticData;

    private readonly Random _random;

    public TappableGenerator(StaticData.StaticData staticData, ILogger<TappableGenerator> logger)
    {
        _staticData = staticData;

        if (_staticData.TappablesConfig.Tappables.Length == 0)
        {
            LogNoTappableConfigsProvided(logger);
        }

        _random = new Random();
    }

    public TimeSpan GetMaxTappableLifetime()
        => MAX_DELAY + MAX_DURATION + TimeSpan.FromSeconds(30);

    public IEnumerable<Tappable> GenerateTappables(int tileX, int tileY, DateTimeOffset currentTime)
    {
        if (_staticData.TappablesConfig.Tappables.Length == 0)
        {
            return [];
        }

#pragma warning disable CA5394 // Do not use insecure randomness - idc
        int count = _random.Next(MIN_COUNT, MAX_COUNT + 1);

        var tappables = new List<Tappable>(count);
        Span<float> tileBounds = stackalloc float[4];
        for (; count > 0; count--)
        {
            var spawnDelay = TimeSpan.FromTicks(_random.NextInt64(MIN_DELAY.Ticks, MAX_DELAY.Ticks + 1));
            var duration = TimeSpan.FromTicks(_random.NextInt64(MIN_DURATION.Ticks, MAX_DURATION.Ticks + 1));

            TappablesConfig.TappableConfig tappableConfig = _staticData.TappablesConfig.Tappables[_random.Next(0, _staticData.TappablesConfig.Tappables.Length)];

            GetTileBounds(tileX, tileY, tileBounds);
            float lat = _random.NextSingle(tileBounds[1], tileBounds[0]);
            float lon = _random.NextSingle(tileBounds[2], tileBounds[3]);

            int dropSetIndex = _random.Next(0, tappableConfig.DropSets.Select(dropSet => dropSet.Chance).Sum());
            TappablesConfig.TappableConfig.DropSetR? dropSet = null;

            foreach (TappablesConfig.TappableConfig.DropSetR dropSet1 in tappableConfig.DropSets)
            {
                dropSet = dropSet1;
                dropSetIndex -= dropSet1.Chance;
                if (dropSetIndex <= 0)
                {
                    break;
                }
            }

            if (dropSet is null)
            {
                throw new InvalidOperationException();
            }

            var items = new List<Tappable.Item>(dropSet.Items.Length);

            foreach (var itemId in dropSet.Items)
            {
                TappablesConfig.TappableConfig.ItemCount itemCount = tappableConfig.ItemCounts[itemId];
                items.Add(new Tappable.Item(itemId, _random.Next(itemCount.Min, itemCount.Max + 1)));
#pragma warning restore CA5394 // Do not use insecure randomness
            }

            var rarity = Tappable.RarityE.FromStaticData(items.Max(item => _staticData.Catalog.ItemsCatalog.GetItem(item.Id)!.Rarity));

            var tappable = new Tappable(
                Guid.CreateVersion7(),
                lat,
                lon,
                currentTime + spawnDelay,
                duration,
                tappableConfig.Icon,
                rarity,
                [.. items]
            );

            tappables.Add(tappable);
        }

        return tappables;
    }

    private static void GetTileBounds(int tileX, int tileY, Span<float> dest)
    {
        dest[0] = YToLat((float)tileY / (1 << 16));
        dest[1] = YToLat((float)(tileY + 1) / (1 << 16));
        dest[2] = XToLon((float)tileX / (1 << 16));
        dest[3] = XToLon((float)(tileX + 1) / (1 << 16));
    }

    private static float XToLon(float x)
        => (float)MathE.ToDegrees((x * 2.0d - 1.0d) * double.Pi);

    private static float YToLat(float y)
        => (float)MathE.ToDegrees(double.Atan(double.Sinh((1.0d - y * 2.0d) * double.Pi)));

    [LoggerMessage(Level = LogLevel.Warning, Message = "No tappable configs provided")]
    private static partial void LogNoTappableConfigsProvided(ILogger logger);
}