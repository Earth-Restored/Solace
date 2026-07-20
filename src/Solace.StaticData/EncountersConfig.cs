using System.Collections.Immutable;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Solace.StaticData;

public sealed class EncountersConfig
{
    public readonly ImmutableArray<EncounterConfig> Encounters;

    internal EncountersConfig(string dir)
    {
        try
        {
            var encounters = ImmutableArray.CreateBuilder<EncounterConfig>();
            foreach (var file in Directory.EnumerateFiles(dir))
            {
                if (Path.GetExtension(file) != ".json")
                {
                    continue;
                }

                using (var stream = File.OpenRead(file))
                {
                    var encounter = JsonSerializer.Deserialize(stream, AppJsonContext.Default.EncounterConfig);

                    Debug.Assert(encounter is not null);

                    encounters.Add(encounter);
                }
            }

            Encounters = encounters.DrainToImmutable();
        }
        catch (Exception exception)
        {
            throw new StaticDataException(null, exception);
        }
    }

    public sealed record EncounterConfig(
        string Icon,
        EncounterRarity Rarity,
        string EncounterBuildplateId,
        int Duration
    );

    [JsonConverter(typeof(JsonStringEnumConverter<EncounterRarity>))]
    public enum EncounterRarity
    {
        COMMON,
        UNCOMMON,
        RARE,
        EPIC,
        LEGENDARY,
    }
}
