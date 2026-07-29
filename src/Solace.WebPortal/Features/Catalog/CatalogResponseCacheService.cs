using System.Collections.Immutable;
using System.Diagnostics;
using Microsoft.Extensions.Caching.Memory;
using Solace.StaticData;
using Solace.WebPortal.Common.Features.Catalog;
using Item = Solace.StaticData.Catalog.ItemsCatalogR.Item;

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
                    Item.TypeE.BLOCK => ItemDtoType.Block,
                    Item.TypeE.ITEM => ItemDtoType.Item,
                    Item.TypeE.TOOL => ItemDtoType.Tool,
                    Item.TypeE.MOB => ItemDtoType.Mob,
                    Item.TypeE.ENVIRONMENT_BLOCK => ItemDtoType.EnvironmentBlock,
                    Item.TypeE.BOOST => ItemDtoType.Boost,
                    Item.TypeE.ADVENTURE_SCROLL => ItemDtoType.AdventureScroll,
                    _ => throw new UnreachableException(),
                },
                item.Category switch
                {
                    Item.CategoryE.CONSTRUCTION => ItemDtoCategory.Construction,
                    Item.CategoryE.EQUIPMENT => ItemDtoCategory.Equipment,
                    Item.CategoryE.ITEMS => ItemDtoCategory.Items,
                    Item.CategoryE.MOBS => ItemDtoCategory.Mobs,
                    Item.CategoryE.NATURE => ItemDtoCategory.Nature,
                    Item.CategoryE.BOOST_ADVENTURE_XP => ItemDtoCategory.BoostAdventureXP,
                    Item.CategoryE.BOOST_CRAFTING => ItemDtoCategory.BoostCrafting,
                    Item.CategoryE.BOOST_DEFENSE => ItemDtoCategory.BoostDefense,
                    Item.CategoryE.BOOST_EATING => ItemDtoCategory.BoostEating,
                    Item.CategoryE.BOOST_HEALTH => ItemDtoCategory.BoostHealth,
                    Item.CategoryE.BOOST_HOARDING => ItemDtoCategory.BoostHoarding,
                    Item.CategoryE.BOOST_ITEM_XP => ItemDtoCategory.BoostItemXP,
                    Item.CategoryE.BOOST_MINING_SPEED => ItemDtoCategory.BoostMiningSpeed,
                    Item.CategoryE.BOOST_RETENTION => ItemDtoCategory.BoostRetention,
                    Item.CategoryE.BOOST_SMELTING => ItemDtoCategory.BoostSmelting,
                    Item.CategoryE.BOOST_STRENGTH => ItemDtoCategory.BoostStrength,
                    Item.CategoryE.BOOST_TAPPABLE_RADIUS => ItemDtoCategory.BoostTappableRadius,
                    _ => throw new UnreachableException(),
                },
                item.Rarity switch
                {
                    Item.RarityE.COMMON => ItemDtoRarity.Common,
                    Item.RarityE.UNCOMMON => ItemDtoRarity.Uncommon,
                    Item.RarityE.RARE => ItemDtoRarity.Rare,
                    Item.RarityE.EPIC => ItemDtoRarity.Epic,
                    Item.RarityE.LEGENDARY => ItemDtoRarity.Legendary,
                    Item.RarityE.OOBE => ItemDtoRarity.OOBE,
                    _ => throw new UnreachableException(),
                },
                item.UseType switch
                {
                    Item.UseTypeE.NONE => ItemDtoUseType.None,
                    Item.UseTypeE.BUILD => ItemDtoUseType.Build,
                    Item.UseTypeE.BUILD_ATTACK => ItemDtoUseType.BuildAttack,
                    Item.UseTypeE.INTERACT => ItemDtoUseType.Interact,
                    Item.UseTypeE.INTERACT_AND_BUILD => ItemDtoUseType.InteractAndBuild,
                    Item.UseTypeE.DESTROY => ItemDtoUseType.Destroy,
                    Item.UseTypeE.USE => ItemDtoUseType.Use,
                    Item.UseTypeE.CONSUME => ItemDtoUseType.Consume,
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