using System.Collections.Immutable;
using System.Diagnostics;
using System.Text.Json;
using Solace.Common;

namespace Solace.StaticData;

public sealed class TappablesConfig
{
    public readonly ImmutableArray<TappableConfig> Tappables;

    internal TappablesConfig(string dir)
    {
        try
        {
            var tappables = ImmutableArray.CreateBuilder<TappableConfig>();
            foreach (string file in Directory.EnumerateFiles(dir))
            {
                if (Path.GetExtension(file) != ".json")
                {
                    continue;
                }

                using (var stream = File.OpenRead(file))
                {
                    var tappable = JsonSerializer.Deserialize(stream, AppJsonContext.Default.TappableConfig);

                    Debug.Assert(tappable is not null);

                    tappables.Add(tappable);
                }
            }

            Tappables = tappables.DrainToImmutable();

            foreach (TappableConfig tappableConfig in Tappables)

            {
                foreach (TappableConfig.DropSetR dropSet in tappableConfig.DropSets)
                {
                    foreach (var itemId in dropSet.Items)
                    {
                        if (!tappableConfig.ItemCounts.ContainsKey(itemId))
                        {
                            throw new StaticDataException($"Tappable config {tappableConfig.Icon} has no item count for item {itemId}");
                        }
                    }
                }
            }
        }
        catch (StaticDataException)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw new StaticDataException(null, exception);
        }
    }

    public sealed record TappableConfig(
        string Icon,
        TappableConfig.DropSetR[] DropSets,
        Dictionary<Guid, TappableConfig.ItemCount> ItemCounts
    )
    {
        public sealed record DropSetR(
            Guid[] Items,
            int Chance
        );

        public sealed record ItemCount(
            int Min,
            int Max
        );
    }
}
