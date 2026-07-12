using System.ComponentModel;
using System.Text.Json.Serialization;

namespace Solace.TappablesGenerator;

internal sealed record Encounter(
    Guid Id,
    float Lat,
    float Lon,
    DateTimeOffset SpawnTime,
    TimeSpan ValidFor,
    string Icon,
    Encounter.RarityE Rarity,
    string EncounterBuildplateId
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

internal static class EncounterRarityExtensions
{
    extension(Encounter.RarityE)
    {
        public static Encounter.RarityE FromStaticData(StaticData.EncountersConfig.EncounterRarity rarity)
            => rarity switch
            {
                StaticData.EncountersConfig.EncounterRarity.COMMON => Encounter.RarityE.COMMON,
                StaticData.EncountersConfig.EncounterRarity.UNCOMMON => Encounter.RarityE.UNCOMMON,
                StaticData.EncountersConfig.EncounterRarity.RARE => Encounter.RarityE.RARE,
                StaticData.EncountersConfig.EncounterRarity.EPIC => Encounter.RarityE.EPIC,
                StaticData.EncountersConfig.EncounterRarity.LEGENDARY => Encounter.RarityE.LEGENDARY,
                _ => throw new InvalidEnumArgumentException(nameof(rarity), (int)rarity, typeof(StaticData.EncountersConfig.EncounterRarity)),
            };
    }
}