using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Solace.Db.Earth;
using Solace.Db.Playfab.Models.Items;
using Solace.WebPortal.Common.Features.Store;

namespace Solace.WebPortal.Features.Store.Items;

public static class ItemDtoUtils
{
    public static bool IsValid(ItemDto? item)
    {
        if (item is null)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(item.Title) || string.IsNullOrWhiteSpace(item.Description))
        {
            return false;
        }

        foreach (var title in item.TitleTranslations)
        {
            if (!Constants.LanguageNames.ContainsKey(title.Key))
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(title.Value))
            {
                return false;
            }
        }

        foreach (var description in item.DescriptionTranslations)
        {
            if (string.IsNullOrWhiteSpace(description.Key) || string.IsNullOrWhiteSpace(description.Value))
            {
                return false;
            }
        }

        switch (item.ItemDataType)
        {
            case ItemDataTypeDto.Buildplate:
                {
                    var buildplate = item.BuildplateData;
                    if (buildplate is null or { Cost: < 0 } or { UnlockLevel: < 1 })
                    {
                        return false;
                    }

                    if (!Enum.IsDefined(buildplate.Rarity))
                    {
                        return false;
                    }
                }

                break;
            case ItemDataTypeDto.InventoryItem:
                {
                    var inventoryItem = item.InventoryItemData;
                    if (inventoryItem is null or { Cost: < 0 } or { Amount: < 1 })
                    {
                        return false;
                    }

                    if (!Enum.IsDefined(inventoryItem.Rarity))
                    {
                        return false;
                    }
                }

                break;
            default:
                return false;
        }

        return true;
    }

    public static void CreateTags(List<string> tags, Guid id, ItemDto item, StaticData.Catalog.ItemsCatalogR staticData)
    {
        tags.Clear();
        tags.Add(id.ToString());

        switch (item.ItemDataType)
        {
            case ItemDataTypeDto.Buildplate:
                {
                    Debug.Assert(item.BuildplateData is not null);

                    foreach (var userTag in item.BuildplateData.Tags)
                    {
                        tags.Add($"tag.{userTag.Trim()}");
                    }

                    if (item.BuildplateData.Is1Player)
                    {
                        tags.Add("1P");
                    }
                }

                break;
            case ItemDataTypeDto.InventoryItem:
                {
                    Debug.Assert(item.InventoryItemData is not null);

                    var isBoost = false;
                    if (staticData.TryGetItem(item.InventoryItemData.ItemId, out var inventoryItem))
                    {
                        isBoost = inventoryItem.Category is
                            StaticData.Catalog.ItemsCatalogR.Item.CategoryE.BOOST_ADVENTURE_XP or
                            StaticData.Catalog.ItemsCatalogR.Item.CategoryE.BOOST_CRAFTING or
                            StaticData.Catalog.ItemsCatalogR.Item.CategoryE.BOOST_DEFENSE or
                            StaticData.Catalog.ItemsCatalogR.Item.CategoryE.BOOST_EATING or
                            StaticData.Catalog.ItemsCatalogR.Item.CategoryE.BOOST_HEALTH or
                            StaticData.Catalog.ItemsCatalogR.Item.CategoryE.BOOST_HOARDING or
                            StaticData.Catalog.ItemsCatalogR.Item.CategoryE.BOOST_ITEM_XP or
                            StaticData.Catalog.ItemsCatalogR.Item.CategoryE.BOOST_MINING_SPEED or
                            StaticData.Catalog.ItemsCatalogR.Item.CategoryE.BOOST_RETENTION or
                            StaticData.Catalog.ItemsCatalogR.Item.CategoryE.BOOST_SMELTING or
                            StaticData.Catalog.ItemsCatalogR.Item.CategoryE.BOOST_STRENGTH or
                            StaticData.Catalog.ItemsCatalogR.Item.CategoryE.BOOST_TAPPABLE_RADIUS;
                    }

                    tags.Add(isBoost ? "Boosts" : "inventoryitem");

                    if (isBoost)
                    {
                        tags.Add(item.InventoryItemData.ItemId.ToString());
                    }
                }

                break;
        }

        if (item.Discount)
        {
            tags.Add("earth_discount");
        }
    }

    public static async Task<ItemDto> MapItemAsync(ItemEF item, EarthDbContext earthDb, StaticData.Catalog.ItemsCatalogR catalog, CancellationToken cancellationToken = default)
        => new(
            item.Id,
            item.Title,
            item.TitleTranslations,
            item.Description,
            item.DescriptionTranslations,
            item.Purchasable,
            item.Tags.Contains("earth_discount", StringComparer.Ordinal),
            item.StartDate,
            item.ThumbnailImageId,
            item.Data is BuildplateDataEF ? ItemDataTypeDto.Buildplate : ItemDataTypeDto.InventoryItem,
            item.Data is BuildplateDataEF buildplateData
                ? new BuildplateDto(
                    buildplateData.BuildplateId,
                    (await earthDb.TemplateBuildplates
                        .AsNoTracking()
                        .Where(template => template.Id == buildplateData.BuildplateId)
                        .Select(template => new { template.Name })
                        .FirstOrDefaultAsync(cancellationToken))
                        ?.Name,
                    buildplateData.Cost,
                    buildplateData.UnlockLevel,
                    item.Tags.Contains("1P", StringComparer.Ordinal),
                    item.Tags
                        .Where(tag => tag.StartsWith("tag.", StringComparison.Ordinal))
                        .Select(tag => tag["tag.".Length..]),
                    MapRarity(buildplateData.Rarity),
                    buildplateData.Version
                )
                : null,
            item.Data is InventoryItemDataEF inventoryData
                ? new InventoryItemDto(
                    inventoryData.ItemId,
                    catalog.TryGetItem(inventoryData.ItemId, out var catalogItem) ? catalogItem.Name : null,
                    inventoryData.Cost,
                    inventoryData.Amount,
                    MapRarity(inventoryData.Rarity),
                    inventoryData.Version
                )
                : null
        );

    public static RarityDto MapRarity(RarityEF rarity)
        => rarity switch
        {
            RarityEF.None => RarityDto.None,
            RarityEF.Common => RarityDto.Common,
            RarityEF.Uncommon => RarityDto.Uncommon,
            RarityEF.Epic => RarityDto.Epic,
            RarityEF.Rare => RarityDto.Rare,
            RarityEF.Legendary => RarityDto.Legendary,
            _ => throw new UnreachableException(),
        };

    public static RarityEF MapRarity(RarityDto rarity)
        => rarity switch
        {
            RarityDto.None => RarityEF.None,
            RarityDto.Common => RarityEF.Common,
            RarityDto.Uncommon => RarityEF.Uncommon,
            RarityDto.Epic => RarityEF.Epic,
            RarityDto.Rare => RarityEF.Rare,
            RarityDto.Legendary => RarityEF.Legendary,
            _ => throw new UnreachableException(),
        };
}
