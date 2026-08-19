using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using Solace.WebPortal.Common.Features.Store;

namespace Solace.WebPortal.Client.Features.Store.Models;

public sealed class ItemViewModel
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required] public string Title { get; set; } = string.Empty;

    public Dictionary<string, string> TitleTranslations = [];

    [Required] public string Description { get; set; } = string.Empty;

    public Dictionary<string, string> DescriptionTranslations = [];

    public bool Purchasable { get; set; } = true;

    public bool Discount { get; set; }

    public DateTimeOffset StartDate { get; set; } = DateTimeOffset.UtcNow;

    public Guid? ThumbnailImageId { get; set; }

    public ItemDataType ItemDataType { get; set; } = ItemDataType.Buildplate;

    public BuildplateViewModel Buildplate { get; set; } = new();

    public InventoryItemViewModel InventoryItem { get; set; } = new();

    public static ItemViewModel FromDto(ItemDto item)
    {
        var viewModel = new ItemViewModel()
        {
            Id = item.Id,
            Title = item.Title,
            TitleTranslations = item.TitleTranslations.ToDictionary(StringComparer.Ordinal),
            Description = item.Description,
            DescriptionTranslations = item.DescriptionTranslations.ToDictionary(StringComparer.Ordinal),
            Purchasable = item.Purchasable,
            Discount = item.Discount,
            StartDate = item.StartDate,
            ThumbnailImageId = item.ThumbnailImageId,
            ItemDataType = item.ItemDataType switch
            {
                ItemDataTypeDto.Buildplate => ItemDataType.Buildplate,
                ItemDataTypeDto.InventoryItem => ItemDataType.InventoryItem,
                _ => throw new UnreachableException(),
            },
        };

        switch (item.ItemDataType)
        {
            case ItemDataTypeDto.Buildplate:
                var buildplateData = item.BuildplateData;
                Debug.Assert(buildplateData is not null);

                viewModel.Buildplate.BuildplateId = buildplateData.BuildplateId;
                viewModel.Buildplate.BuildplateName = buildplateData.BuildplateName;
                viewModel.Buildplate.Cost = buildplateData.Cost;
                viewModel.Buildplate.UnlockLevel = buildplateData.UnlockLevel;
                viewModel.Buildplate.Is1Player = buildplateData.Is1Player;
                viewModel.Buildplate.Tags = [.. buildplateData.Tags];
                viewModel.Buildplate.Rarity = MapRarity(buildplateData.Rarity);
                viewModel.Buildplate.Version = buildplateData.Version.ToString();
                break;
            case ItemDataTypeDto.InventoryItem:
                var inventoryData = item.InventoryItemData;
                Debug.Assert(inventoryData is not null);

                viewModel.InventoryItem.ItemId = inventoryData.ItemId;
                viewModel.InventoryItem.ItemName = inventoryData.ItemName;
                viewModel.InventoryItem.Cost = inventoryData.Cost;
                viewModel.InventoryItem.Amount = inventoryData.Amount;
                viewModel.InventoryItem.Rarity = MapRarity(inventoryData.Rarity);
                viewModel.InventoryItem.Version = inventoryData.Version.ToString();
                break;
            default:
                throw new UnreachableException();
        }

        return viewModel;

        static Rarity MapRarity(RarityDto rarity)
        {
            return rarity switch
            {
                RarityDto.None => Rarity.None,
                RarityDto.Common => Rarity.Common,
                RarityDto.Uncommon => Rarity.Uncommon,
                RarityDto.Epic => Rarity.Epic,
                RarityDto.Rare => Rarity.Rare,
                RarityDto.Legendary => Rarity.Legendary,
                _ => throw new UnreachableException(),
            };
        }
    }

    public ItemDto ToDto()
    {
        return new(
            Id,
            Title,
            TitleTranslations,
            Description,
            DescriptionTranslations,
            Purchasable,
            Discount,
            StartDate,
            ThumbnailImageId,
            ItemDataType switch
            {
                ItemDataType.Buildplate => ItemDataTypeDto.Buildplate,
                ItemDataType.InventoryItem => ItemDataTypeDto.InventoryItem,
                _ => throw new UnreachableException(),
            },
            ItemDataType is ItemDataType.Buildplate
            ? new BuildplateDto(Buildplate.BuildplateId, null, Buildplate.Cost, Buildplate.UnlockLevel, Buildplate.Is1Player, Buildplate.Tags, MapRarity(Buildplate.Rarity), Version.Parse(Buildplate.Version))
            : null,
            ItemDataType is ItemDataType.InventoryItem
            ? new InventoryItemDto(InventoryItem.ItemId, null, InventoryItem.Cost, InventoryItem.Amount, MapRarity(InventoryItem.Rarity), Version.Parse(InventoryItem.Version))
            : null);

        static RarityDto MapRarity(Rarity rarity)
        {
            return rarity switch
            {
                Rarity.None => RarityDto.None,
                Rarity.Common => RarityDto.Common,
                Rarity.Uncommon => RarityDto.Uncommon,
                Rarity.Epic => RarityDto.Epic,
                Rarity.Rare => RarityDto.Rare,
                Rarity.Legendary => RarityDto.Legendary,
                _ => throw new UnreachableException(),
            };
        }
    }
}
