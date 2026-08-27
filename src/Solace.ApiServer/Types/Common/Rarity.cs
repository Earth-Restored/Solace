using System.ComponentModel;
using System.Text.Json.Serialization;

namespace Solace.ApiServer.Types.Common;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum Rarity
{
    [JsonStringEnumMemberName("Common")] COMMON,
    [JsonStringEnumMemberName("Uncommon")] UNCOMMON,
    [JsonStringEnumMemberName("Rare")] RARE,
    [JsonStringEnumMemberName("Epic")] EPIC,
    [JsonStringEnumMemberName("Legendary")] LEGENDARY,
    [JsonStringEnumMemberName("oobe")] OOBE,
}

public static class RarityExtensions
{
    extension(Rarity)
    {
        public static Rarity FromStaticData(StaticData.Catalog.ItemsCatalogR.Item.RarityE rarity)
            => rarity switch
            {
                StaticData.Catalog.ItemsCatalogR.Item.RarityE.COMMON => Rarity.COMMON,
                StaticData.Catalog.ItemsCatalogR.Item.RarityE.UNCOMMON => Rarity.UNCOMMON,
                StaticData.Catalog.ItemsCatalogR.Item.RarityE.RARE => Rarity.RARE,
                StaticData.Catalog.ItemsCatalogR.Item.RarityE.EPIC => Rarity.EPIC,
                StaticData.Catalog.ItemsCatalogR.Item.RarityE.LEGENDARY => Rarity.LEGENDARY,
                StaticData.Catalog.ItemsCatalogR.Item.RarityE.OOBE => Rarity.OOBE,
                _ => throw new InvalidEnumArgumentException(nameof(rarity), (int)rarity, typeof(StaticData.Catalog.ItemsCatalogR.Item.RarityE)),
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
