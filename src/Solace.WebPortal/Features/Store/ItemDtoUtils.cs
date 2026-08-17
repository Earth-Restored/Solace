using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Solace.Db.Earth;
using Solace.Db.Playfab.Models.Items;
using Solace.WebPortal.Common.Features.Store;

namespace Solace.WebPortal.Features.Store;

public static class ItemDtoUtils
{
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
                ? new BuildplateDto(buildplateData.BuildplateId, (await earthDb.TemplateBuildplates.AsNoTracking().Where(template => template.Id == buildplateData.BuildplateId).Select(template => new { template.Name }).FirstOrDefaultAsync(cancellationToken))?.Name, buildplateData.Cost, buildplateData.UnlockLevel, MapRarity(buildplateData.Rarity), buildplateData.Version)
                : null,
            item.Data is InventoryItemDataEF inventoryData
                ? new InventoryItemDto(inventoryData.ItemId, catalog.TryGetItem(inventoryData.ItemId, out var catalogItem) ? catalogItem.Name : null, inventoryData.Cost, inventoryData.Amount, MapRarity(inventoryData.Rarity), inventoryData.Version)
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
}
