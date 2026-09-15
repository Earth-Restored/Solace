using System.ComponentModel;
using System.Text.Json.Serialization;

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
[JsonConverter(typeof(JsonStringEnumConverter<TappableRarity>))]
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
        public static TappableRarity FromStaticData(StaticData.Catalog.ItemsCatalogR.ItemRarity rarity)
            => rarity switch
            {
                StaticData.Catalog.ItemsCatalogR.ItemRarity.COMMON => TappableRarity.COMMON,
                StaticData.Catalog.ItemsCatalogR.ItemRarity.UNCOMMON => TappableRarity.UNCOMMON,
                StaticData.Catalog.ItemsCatalogR.ItemRarity.RARE => TappableRarity.RARE,
                StaticData.Catalog.ItemsCatalogR.ItemRarity.EPIC => TappableRarity.EPIC,
                StaticData.Catalog.ItemsCatalogR.ItemRarity.LEGENDARY => TappableRarity.LEGENDARY,
                _ => throw new InvalidEnumArgumentException(nameof(rarity), (int)rarity, typeof(StaticData.Catalog.ItemsCatalogR.ItemRarity)),
            };
    }
}
#pragma warning restore MA0048 // File name must match type name
