using System.Collections.Immutable;
using System.Diagnostics;
using Microsoft.Extensions.Caching.Memory;
using Solace.StaticData;
using Solace.WebPortal.Common.Features.Catalog;
using Item = Solace.StaticData.Catalog.ItemsCatalogR.Item;
using ItemType = Solace.StaticData.Catalog.ItemsCatalogR.ItemType;
using ItemCategory = Solace.StaticData.Catalog.ItemsCatalogR.ItemCategory;
using ItemRarity = Solace.StaticData.Catalog.ItemsCatalogR.ItemRarity;
using ItemUseType = Solace.StaticData.Catalog.ItemsCatalogR.ItemUseType;

namespace Solace.WebPortal.Features.Catalog;

public sealed class CatalogResponseCacheService
{
    private readonly StaticData.Catalog _catalog;
    private readonly IMemoryCache _cache;

    public CatalogResponseCacheService(StaticDataProvider staticData, IMemoryCache cache)
    {
        _catalog = staticData.Catalog;
        _cache = cache;
    }

    // todo: make the cache duration configurable
    public ImmutableArray<ItemDto> GetItemsCatalog()
    {
        var lazy = _cache.GetOrCreate("Catalog_ItemsCatalog", entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(15);

            return new Lazy<ImmutableArray<ItemDto>>(CreateItemsCatalog);
        });

        Debug.Assert(lazy is not null);

        return lazy.Value;
    }

    private ImmutableArray<ItemDto> CreateItemsCatalog()
    {
        var builder = ImmutableArray.CreateBuilder<ItemDto>(_catalog.ItemsCatalog.Items.Length);

        foreach (var item in _catalog.ItemsCatalog.Items.AsSpan())
        {
            builder.Add(new ItemDto(
                item.Id,
                item.Name,
                item.Aux,
                item.Stackable,
                item.Type switch
                {
                    ItemType.BLOCK => ItemDtoType.Block,
                    ItemType.ITEM => ItemDtoType.Item,
                    ItemType.TOOL => ItemDtoType.Tool,
                    ItemType.MOB => ItemDtoType.Mob,
                    ItemType.ENVIRONMENT_BLOCK => ItemDtoType.EnvironmentBlock,
                    ItemType.BOOST => ItemDtoType.Boost,
                    ItemType.ADVENTURE_SCROLL => ItemDtoType.AdventureScroll,
                    _ => throw new UnreachableException(),
                },
                item.Category switch
                {
                    ItemCategory.CONSTRUCTION => ItemDtoCategory.Construction,
                    ItemCategory.EQUIPMENT => ItemDtoCategory.Equipment,
                    ItemCategory.ITEMS => ItemDtoCategory.Items,
                    ItemCategory.MOBS => ItemDtoCategory.Mobs,
                    ItemCategory.NATURE => ItemDtoCategory.Nature,
                    ItemCategory.BOOST_ADVENTURE_XP => ItemDtoCategory.BoostAdventureXP,
                    ItemCategory.BOOST_CRAFTING => ItemDtoCategory.BoostCrafting,
                    ItemCategory.BOOST_DEFENSE => ItemDtoCategory.BoostDefense,
                    ItemCategory.BOOST_EATING => ItemDtoCategory.BoostEating,
                    ItemCategory.BOOST_HEALTH => ItemDtoCategory.BoostHealth,
                    ItemCategory.BOOST_HOARDING => ItemDtoCategory.BoostHoarding,
                    ItemCategory.BOOST_ITEM_XP => ItemDtoCategory.BoostItemXP,
                    ItemCategory.BOOST_MINING_SPEED => ItemDtoCategory.BoostMiningSpeed,
                    ItemCategory.BOOST_RETENTION => ItemDtoCategory.BoostRetention,
                    ItemCategory.BOOST_SMELTING => ItemDtoCategory.BoostSmelting,
                    ItemCategory.BOOST_STRENGTH => ItemDtoCategory.BoostStrength,
                    ItemCategory.BOOST_TAPPABLE_RADIUS => ItemDtoCategory.BoostTappableRadius,
                    _ => throw new UnreachableException(),
                },
                item.Rarity switch
                {
                    ItemRarity.COMMON => ItemDtoRarity.Common,
                    ItemRarity.UNCOMMON => ItemDtoRarity.Uncommon,
                    ItemRarity.RARE => ItemDtoRarity.Rare,
                    ItemRarity.EPIC => ItemDtoRarity.Epic,
                    ItemRarity.LEGENDARY => ItemDtoRarity.Legendary,
                    ItemRarity.OOBE => ItemDtoRarity.OOBE,
                    _ => throw new UnreachableException(),
                },
                item.UseType switch
                {
                    ItemUseType.NONE => ItemDtoUseType.None,
                    ItemUseType.BUILD => ItemDtoUseType.Build,
                    ItemUseType.BUILD_ATTACK => ItemDtoUseType.BuildAttack,
                    ItemUseType.INTERACT => ItemDtoUseType.Interact,
                    ItemUseType.INTERACT_AND_BUILD => ItemDtoUseType.InteractAndBuild,
                    ItemUseType.DESTROY => ItemDtoUseType.Destroy,
                    ItemUseType.USE => ItemDtoUseType.Use,
                    ItemUseType.CONSUME => ItemDtoUseType.Consume,
                    _ => throw new UnreachableException(),
                },
                item.BlockInfo is { } blockInfo ? new ItemDtoBlockInfo(blockInfo.BreakingHealth, blockInfo.EfficiencyCategory) : null,
                item.ToolInfo is { } toolInfo ? new ItemDtoToolInfo(toolInfo.BlockDamage, toolInfo.MobDamage, toolInfo.MaxWear, toolInfo.EfficiencyCategory) : null,
                item.ConsumeInfo is { } consumeInfo ? new ItemDtoConsumeInfo(consumeInfo.Heal, consumeInfo.ReturnItemId) : null,
                item.FuelInfo is { } fuelInfo ? new ItemDtoFuelInfo(fuelInfo.BurnTime, fuelInfo.HeatPerSecond, fuelInfo.ReturnItemId) : null,
                item.BoostInfo is { } boostInfo
                    ? new ItemDtoBoostInfo(boostInfo.Name,
                        boostInfo.Level,
                        boostInfo.Type switch
                        {
                            Item.BoostInfoType.POTION => ItemDtoBoostInfoType.Potion,
                            Item.BoostInfoType.INVENTORY_ITEM => ItemDtoBoostInfoType.InventoryItem,
                            _ => throw new UnreachableException(),
                        },
                        boostInfo.Duration
                        )
                    : null,
                new ItemDtoExperience(item.Experience.Tappable, item.Experience.Encounter, item.Experience.Crafting, item.Experience.Journal)
            ));
        }

        return builder.MoveToImmutable();
    }
}