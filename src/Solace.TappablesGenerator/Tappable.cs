using System.ComponentModel;
using static Solace.TappablesGenerator.Tappable;

namespace Solace.TappablesGenerator;

public sealed record Tappable(
    Guid Id,
    float Lat,
    float Lon,
    long SpawnTime,
    long ValidFor,
    string Icon,
    RarityE Rarity,
    Item[] Items
)
{
    public enum RarityE
    {
        COMMON,
        UNCOMMON,
        RARE,
        EPIC,
        LEGENDARY
    }

    public sealed record Item(
        string Id,
        int Count
    );
}

public static class TappableRarityExtensions
{
    extension(Tappable.RarityE)
    {
        public static Tappable.RarityE FromStaticData(StaticData.Catalog.ItemsCatalogR.Item.RarityE rarity)
            => rarity switch
            {
                StaticData.Catalog.ItemsCatalogR.Item.RarityE.COMMON => Tappable.RarityE.COMMON,
                StaticData.Catalog.ItemsCatalogR.Item.RarityE.UNCOMMON => Tappable.RarityE.UNCOMMON,
                StaticData.Catalog.ItemsCatalogR.Item.RarityE.RARE => Tappable.RarityE.RARE,
                StaticData.Catalog.ItemsCatalogR.Item.RarityE.EPIC => Tappable.RarityE.EPIC,
                StaticData.Catalog.ItemsCatalogR.Item.RarityE.LEGENDARY => Tappable.RarityE.LEGENDARY,
                _ => throw new InvalidEnumArgumentException(nameof(rarity), (int)rarity, typeof(StaticData.Catalog.ItemsCatalogR.Item.RarityE)),
            };
    }
}