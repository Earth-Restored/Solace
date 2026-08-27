using System.ComponentModel;
using System.Text.Json.Serialization;

namespace Solace.TappablesGenerator;

public sealed record Encounter(
    Guid Id,
    float Lat,
    float Lon,
    long SpawnTime,
    long ValidFor,
    string Icon,
    Encounter.RarityE Rarity,
    string EncounterBuildplateId
)
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum RarityE
    {
        COMMON,
        UNCOMMON,
        RARE,
        EPIC,
        LEGENDARY
    }
}

public static class EncounterRarityExtensions
{
    extension(Encounter.RarityE)
    {
        public static Encounter.RarityE FromStaticData(StaticData.EncountersConfig.EncounterConfig.RarityE rarity)
            => rarity switch
            {
                StaticData.EncountersConfig.EncounterConfig.RarityE.COMMON => Encounter.RarityE.COMMON,
                StaticData.EncountersConfig.EncounterConfig.RarityE.UNCOMMON => Encounter.RarityE.UNCOMMON,
                StaticData.EncountersConfig.EncounterConfig.RarityE.RARE => Encounter.RarityE.RARE,
                StaticData.EncountersConfig.EncounterConfig.RarityE.EPIC => Encounter.RarityE.EPIC,
                StaticData.EncountersConfig.EncounterConfig.RarityE.LEGENDARY => Encounter.RarityE.LEGENDARY,
                _ => throw new InvalidEnumArgumentException(nameof(rarity), (int)rarity, typeof(StaticData.EncountersConfig.EncounterConfig.RarityE)),
            };
    }
}