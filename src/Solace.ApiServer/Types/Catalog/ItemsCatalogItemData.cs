using System.Collections;
using static Solace.ApiServer.Types.Catalog.ItemsCatalogItemData;

namespace Solace.ApiServer.Types.Catalog;

internal sealed record ItemsCatalogItemData(
       string Name,
       int? Aux,
       string Type,
       string UseType,
       double? TapSpeed,
       double? Heal,
       double? Nutrition,
       double? MobDamage,
       double? BlockDamage,
       double? Health,
       BlockMetadataR? BlockMetadata,
       ItemMetadataR? ItemMetadata,
       BoostMetadata? BoostMetadata,
       JournalMetadataR? JournalMetadata,
       AudioMetadataR? AudioMetadata,
       IDictionary ClientProperties
   )
{
    internal sealed record BlockMetadataR(
        double? Health,
        string? EfficiencyCategory
    );

    internal sealed record ItemMetadataR(
        string UseType,
        string AlternativeUseType,
        double? MobDamage,
        double? BlockDamage,
        double? WeakDamage,
        double? Nutrition,
        double? Heal,
        string? EfficiencyType,
        double? MaxHealth
    );

    internal sealed record JournalMetadataR(
        string GroupKey,
        int Experience,
        int Order,
        string Behavior,
        string Biome
    );

    internal sealed record AudioMetadataR(
        Dictionary<string, string> Sounds,
        string DefaultSound
    );
}
