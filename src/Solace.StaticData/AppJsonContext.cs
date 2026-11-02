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
[JsonSerializable(typeof(SeasonsCatalog.SeasonDefinition))]
[JsonSerializable(typeof(SeasonsCatalog.SeasonChallengeDefinition))]
[JsonSerializable(typeof(LogicalCondition))]
[JsonSerializable(typeof(ObjectiveType))]
[JsonSerializable(typeof(SeasonsCatalog.Rewards), TypeInfoPropertyName = "SeasonsRewards")]
[JsonSerializable(typeof(SeasonsCatalog.Rewards.RewardItem), TypeInfoPropertyName = "SeasonsRewardItem")]
[JsonSerializable(typeof(SeasonsCatalog.Rewards.RewardChallenge), TypeInfoPropertyName = "SeasonsRewardChallenge")]
[JsonSerializable(typeof(SeasonsCatalog.Rewards.RewardUtilityBlock), TypeInfoPropertyName = "SeasonsRewardUtilityBlock")]
internal sealed partial class AppJsonContext : JsonSerializerContext
{
}