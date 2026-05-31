using System.Collections.Immutable;
using System.Diagnostics;
using System.Text.Json.Serialization;
using Solace.Common;

namespace Solace.StaticData;

public sealed class AdventuresConfig
{
    private static readonly string[] DefaultFolders = ["common", "uncommon", "rare", "epic", "legendary", "oobe"];

    public readonly AdventureSpawnConfig SpawnConfig;
    private readonly Dictionary<string, ImmutableArray<AdventureBuildplate>> _buildplatesByFolder = [];

    internal AdventuresConfig(string dir)
    {
        try
        {
            SpawnConfig = LoadSpawnConfig(dir);

            HashSet<string> folders = [.. DefaultFolders];
            foreach (AdventureCrystalType crystalType in SpawnConfig.CrystalTypes)
            {
                folders.Add(crystalType.Folder);
            }

            foreach (string folder in folders)
            {
                string buildplatesFile = Path.Combine(dir, folder, $"{folder}-buildplates.json");
                if (!File.Exists(buildplatesFile))
                {
                    continue;
                }

                using var stream = File.OpenRead(buildplatesFile);
                AdventureBuildplatesFile? buildplates = Json.Deserialize<AdventureBuildplatesFile>(stream);
                Debug.Assert(buildplates is not null);

                _buildplatesByFolder[folder] = [.. buildplates.Buildplates
                    .Where(buildplate => !string.IsNullOrWhiteSpace(buildplate.TemplateId))
                    .Select(buildplate => buildplate with
                    {
                        TemplateId = Path.GetFileNameWithoutExtension(buildplate.TemplateId),
                        Weight = int.Max(0, buildplate.Weight)
                    })
                    .Where(buildplate => buildplate.Weight > 0)];
            }
        }
        catch (Exception exception)
        {
            throw new StaticDataException(null, exception);
        }
    }

    public bool CanSpawn => SpawnConfig.CrystalTypes.Length > 0 && SpawnConfig.MaxCount > 0;

    public AdventureCrystalType? PickCrystalType(Random random)
        => PickWeighted(SpawnConfig.CrystalTypes, item => item.PickWeight, random);

    public string? PickTemplateForFolder(string folder, Random random)
    {
        if (!_buildplatesByFolder.TryGetValue(folder, out ImmutableArray<AdventureBuildplate> buildplates) || buildplates.Length == 0)
        {
            return null;
        }

        return PickWeighted(buildplates, buildplate => buildplate.Weight, random)?.TemplateId;
    }

    public string? TryPickTemplateForCrystalItem(string itemName, Random random)
    {
        string normalizedName = itemName.StartsWith("minecraft:", StringComparison.OrdinalIgnoreCase)
            ? itemName["minecraft:".Length..]
            : itemName;

        const string prefix = "adventure_crystal_";
        if (!normalizedName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        string folder = normalizedName[prefix.Length..];
        return PickTemplateForFolder(folder, random);
    }

    private static AdventureSpawnConfig LoadSpawnConfig(string dir)
    {
        string spawnConfigFile = Path.Combine(dir, "adventures-spawn.json");
        if (!File.Exists(spawnConfigFile))
        {
            return AdventureSpawnConfig.Disabled;
        }

        using var stream = File.OpenRead(spawnConfigFile);
        AdventureSpawnConfig? spawnConfig = Json.Deserialize<AdventureSpawnConfig>(stream);
        Debug.Assert(spawnConfig is not null);
        return spawnConfig;
    }

    private static T? PickWeighted<T>(IReadOnlyList<T> items, Func<T, int> weightSelector, Random random)
    {
        int totalWeight = items.Sum(weightSelector);
        if (totalWeight <= 0)
        {
            return default;
        }

        int roll = random.Next(0, totalWeight);
        foreach (T item in items)
        {
            roll -= weightSelector(item);
            if (roll < 0)
            {
                return item;
            }
        }

        return items[^1];
    }

    public sealed record AdventureSpawnConfig(
        int MinCount,
        int MaxCount,
        long MinSpawnDelayMs,
        long MaxSpawnDelayMs,
        long MinDurationMs,
        long MaxDurationMs,
        int ChancePerSpawnCycle,
        AdventureCrystalType[] CrystalTypes
    )
    {
        public static AdventureSpawnConfig Disabled => new(0, 0, 0, 0, 0, 0, 0, []);
    }

    public sealed record AdventureCrystalType(
        string Folder,
        string Icon,
        AdventureCrystalType.RarityE Rarity,
        int PickWeight
    )
    {
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public enum RarityE
        {
            COMMON,
            UNCOMMON,
            RARE,
            EPIC,
            LEGENDARY,
            OOBE
        }
    }

    private sealed record AdventureBuildplatesFile(
        AdventureBuildplate[] Buildplates
    );

    private sealed record AdventureBuildplate(
        string TemplateId,
        int Weight
    );
}
