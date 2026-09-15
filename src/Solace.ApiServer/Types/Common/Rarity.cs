using System.ComponentModel;
using System.Text.Json.Serialization;

namespace Solace.ApiServer.Types.Common;

[JsonConverter(typeof(JsonStringEnumConverter<Rarity>))]
internal enum Rarity
{
    [JsonStringEnumMemberName("Common")] COMMON,
    [JsonStringEnumMemberName("Uncommon")] UNCOMMON,
    [JsonStringEnumMemberName("Rare")] RARE,
    [JsonStringEnumMemberName("Epic")] EPIC,
    [JsonStringEnumMemberName("Legendary")] LEGENDARY,
    [JsonStringEnumMemberName("oobe")] OOBE,
}

#pragma warning disable MA0048 // File name must match type name
internal static class RarityExtensions
#pragma warning restore MA0048 // File name must match type name
{
    extension(Rarity)
    {
        public static Rarity FromStaticData(StaticData.Catalog.ItemsCatalogR.ItemRarity rarity)
            => rarity switch
            {
                StaticData.Catalog.ItemsCatalogR.ItemRarity.COMMON => Rarity.COMMON,
                StaticData.Catalog.ItemsCatalogR.ItemRarity.UNCOMMON => Rarity.UNCOMMON,
                StaticData.Catalog.ItemsCatalogR.ItemRarity.RARE => Rarity.RARE,
                StaticData.Catalog.ItemsCatalogR.ItemRarity.EPIC => Rarity.EPIC,
                StaticData.Catalog.ItemsCatalogR.ItemRarity.LEGENDARY => Rarity.LEGENDARY,
                StaticData.Catalog.ItemsCatalogR.ItemRarity.OOBE => Rarity.OOBE,
                _ => throw new InvalidEnumArgumentException(nameof(rarity), (int)rarity, typeof(StaticData.Catalog.ItemsCatalogR.ItemRarity)),
            };

        public static Rarity FromTappable(Utils.TappablesManager.Tappable.RarityE rarity)
            => rarity switch
            {
                Utils.TappablesManager.Tappable.RarityE.COMMON => Rarity.COMMON,
                Utils.TappablesManager.Tappable.RarityE.UNCOMMON => Rarity.UNCOMMON,
                Utils.TappablesManager.Tappable.RarityE.RARE => Rarity.RARE,
                Utils.TappablesManager.Tappable.RarityE.EPIC => Rarity.EPIC,
                Utils.TappablesManager.Tappable.RarityE.LEGENDARY => Rarity.LEGENDARY,
                _ => throw new InvalidEnumArgumentException(nameof(rarity), (int)rarity, typeof(Utils.TappablesManager.Tappable)),
            };

        public static Rarity FromEncounter(Utils.TappablesManager.Encounter.RarityE rarity)
            => rarity switch
            {
                Utils.TappablesManager.Encounter.RarityE.COMMON => Rarity.COMMON,
                Utils.TappablesManager.Encounter.RarityE.UNCOMMON => Rarity.UNCOMMON,
                Utils.TappablesManager.Encounter.RarityE.RARE => Rarity.RARE,
                Utils.TappablesManager.Encounter.RarityE.EPIC => Rarity.EPIC,
                Utils.TappablesManager.Encounter.RarityE.LEGENDARY => Rarity.LEGENDARY,
                _ => throw new InvalidEnumArgumentException(nameof(rarity), (int)rarity, typeof(Utils.TappablesManager.Tappable)),
            };
    }
}
