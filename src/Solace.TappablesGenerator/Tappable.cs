using System.ComponentModel;

namespace Solace.TappablesGenerator;

internal sealed record Tappable(
    Guid Id,
    float Lat,
    float Lon,
    DateTimeOffset SpawnTime,
    TimeSpan ValidFor,
    string Icon,
    TappableRarity Rarity,
    TappableItem[] Items
);

#pragma warning disable MA0048 // File name must match type name
internal enum TappableRarity
{
    COMMON,
    UNCOMMON,
    RARE,
    EPIC,
    LEGENDARY
}

internal sealed record TappableItem(
    Guid Id,
    int Count
);

internal static class TappableRarityExtensions
{
    extension(TappableRarity)
    {
        public static TappableRarity FromStaticData(StaticData.Catalog.ItemsCatalogR.Item.RarityE rarity)
            => rarity switch
            {
                StaticData.Catalog.ItemsCatalogR.Item.RarityE.COMMON => TappableRarity.COMMON,
                StaticData.Catalog.ItemsCatalogR.Item.RarityE.UNCOMMON => TappableRarity.UNCOMMON,
                StaticData.Catalog.ItemsCatalogR.Item.RarityE.RARE => TappableRarity.RARE,
                StaticData.Catalog.ItemsCatalogR.Item.RarityE.EPIC => TappableRarity.EPIC,
                StaticData.Catalog.ItemsCatalogR.Item.RarityE.LEGENDARY => TappableRarity.LEGENDARY,
                _ => throw new InvalidEnumArgumentException(nameof(rarity), (int)rarity, typeof(StaticData.Catalog.ItemsCatalogR.Item.RarityE)),
            };
    }
}
#pragma warning restore MA0048 // File name must match type name
