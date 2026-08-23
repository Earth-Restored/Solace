using System.Text.Json.Serialization;

namespace Solace.StaticData;

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    PropertyNameCaseInsensitive = true
)]
[JsonSerializable(typeof(AdventuresConfig.AdventureBuildplatesFile))]
[JsonSerializable(typeof(AdventuresConfig.AdventureSpawnConfig))]
[JsonSerializable(typeof(Catalog.ItemEfficiencyCategoriesCatalogR.EfficiencyCategory[]))]
[JsonSerializable(typeof(Catalog.ItemJournalGroupsCatalogR.JournalGroup[]))]
[JsonSerializable(typeof(Catalog.ItemsCatalogR.Item[]))]
[JsonSerializable(typeof(Catalog.NFCBoostsCatalogR.NFCBoostsCatalogFile))]
[JsonSerializable(typeof(Catalog.RecipesCatalogR.RecipesCatalogFile))]
[JsonSerializable(typeof(EncountersConfig.EncounterConfig))]
[JsonSerializable(typeof(PlayerLevels.Level))]
[JsonSerializable(typeof(Playfab.Item), TypeInfoPropertyName = "PlayfabItem")]
[JsonSerializable(typeof(Playfab.Tab))]
[JsonSerializable(typeof(StaticBuidplateInfo))]
[JsonSerializable(typeof(TappablesConfig.TappableConfig))]
internal sealed partial class AppJsonContext : JsonSerializerContext
{
}