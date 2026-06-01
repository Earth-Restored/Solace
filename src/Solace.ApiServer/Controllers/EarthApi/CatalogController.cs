using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using System.Text.Json;
using Solace.ApiServer.Types.Catalog;
using Solace.ApiServer.Utils;
using Solace.StaticData;
using CICIBIEType = Solace.StaticData.Catalog.ItemsCatalogR.Item.BoostInfoR.Effect.TypeE;
using CICIBIType = Solace.StaticData.Catalog.ItemsCatalogR.Item.BoostInfoR.TypeE;
using CICICategory = Solace.StaticData.Catalog.ItemsCatalogR.Item.CategoryE;
using CICIJEBehavior = Solace.StaticData.Catalog.ItemsCatalogR.Item.JournalEntryR.BehaviorE;
using CICIJEBiome = Solace.StaticData.Catalog.ItemsCatalogR.Item.JournalEntryR.BiomeE;
using CICIType = Solace.StaticData.Catalog.ItemsCatalogR.Item.TypeE;
using CICIUseType = Solace.StaticData.Catalog.ItemsCatalogR.Item.UseTypeE;
using CIJGCJGParentCollection = Solace.StaticData.Catalog.ItemJournalGroupsCatalogR.JournalGroup.ParentCollectionE;
using CRCCRCategory = Solace.StaticData.Catalog.RecipesCatalogR.CraftingRecipe.CategoryE;
using ItemsCatalog = Solace.ApiServer.Types.Catalog.ItemsCatalog;
using Solace.ApiServer.Types.Common;

namespace Solace.ApiServer.Controllers.EarthApi;

[Authorize]
[ApiVersion("1.1")]
[Route("1/api/v{version:apiVersion}")]
internal sealed class CatalogController : SolaceControllerBase
{
    private readonly Catalog _catalog;

    public CatalogController(StaticData.StaticData staticData)
    {
        _catalog = staticData.Catalog;
    }

    [HttpGet("inventory/catalogv3")]
    public ContentHttpResult GetItemsCatalog()
        => EarthJson(MakeItemsCatalogApiResponse(_catalog));

    [HttpGet("recipes")]
    public ContentHttpResult GetRecipeCatalog()
        => EarthJson(MakeRecipesCatalogApiResponse(_catalog));

    [HttpGet("journal/catalog")]
    public ContentHttpResult GetJournalCatalog()
        => EarthJson(MakeJournalCatalogApiResponse(_catalog));

    [HttpGet("products/catalog")]
    public ContentHttpResult GetNFCBoostsCatalog()
        => EarthJson(MakeNFCBoostsCatalogApiResponse(_catalog));

    [HttpGet("products/getProductInfo")]
    [HttpPost("products/getProductInfo")]
    public async Task<ContentHttpResult> GetProductInfo(CancellationToken cancellationToken)
    {
        HashSet<string> requestedProductIds = ProductIdsFromQuery();
        if (requestedProductIds.Count == 0 && Request.ContentLength is not null and not 0)
        {
            requestedProductIds = await ReadRequestedProductIdsAsync(Request.Body, cancellationToken);
        }

        NFCBoost[] products = MakeNFCBoostsCatalogApiResponse(_catalog);
        NFCBoost[] matchingProducts = requestedProductIds.Count == 0
            ? products
            : [.. products.Where(product => requestedProductIds.Contains(product.Id))];
        string[] invalidProductIds = requestedProductIds.Count == 0
            ? []
            : [.. requestedProductIds.Except(matchingProducts.Select(product => product.Id))];

        return EarthJson(new Dictionary<string, object>
        {
            ["products"] = matchingProducts,
            ["productInfos"] = matchingProducts,
            ["recentlyViewedProductIds"] = matchingProducts.Select(product => product.Id).ToArray(),
            ["invalidProductIds"] = invalidProductIds
        });
    }

    // TODO: cache these?
    private static ItemsCatalog MakeItemsCatalogApiResponse(Catalog catalog)
    {
        ItemsCatalog.ItemR[] items = [.. catalog.ItemsCatalog.Items.Select(item =>
        {
            string categoryString = item.Category switch
            {
                CICICategory.CONSTRUCTION => "Construction",
                CICICategory.EQUIPMENT => "Equipment",
                CICICategory.ITEMS => "Items",
                CICICategory.MOBS => "Mobs",
                CICICategory.NATURE => "Nature",
                CICICategory.BOOST_ADVENTURE_XP => "adventurexp",
                CICICategory.BOOST_CRAFTING => "crafting",
                CICICategory.BOOST_DEFENSE => "defense",
                CICICategory.BOOST_EATING => "eating",
                CICICategory.BOOST_HEALTH => "maxplayerhealth",
                CICICategory.BOOST_HOARDING => "hoarding",
                CICICategory.BOOST_ITEM_XP => "itemxp",
                CICICategory.BOOST_MINING_SPEED => "miningspeed",
                CICICategory.BOOST_RETENTION => "retention",
                CICICategory.BOOST_SMELTING => "smelting",
                CICICategory.BOOST_STRENGTH => "strength",
                CICICategory.BOOST_TAPPABLE_RADIUS => "tappableRadius",
                _ => throw new UnreachableException(),
            };

            string typeString = item.Type switch
            {
                CICIType.BLOCK => "Block",
                CICIType.ITEM => "Item",
                CICIType.TOOL => "Tool",
                CICIType.MOB => "Mob",
                CICIType.ENVIRONMENT_BLOCK => "EnvironmentBlock",
                CICIType.BOOST => "Boost",
                CICIType.ADVENTURE_SCROLL => "AdventureScroll",
                _ => throw new UnreachableException(),
            };

            string useTypeString = item.UseType switch
            {
                CICIUseType.NONE => "None",

                CICIUseType.BUILD => "Build",
                CICIUseType.BUILD_ATTACK => "BuildAttack",
                CICIUseType.INTERACT => "Interact",
                CICIUseType.INTERACT_AND_BUILD => "InteractAndBuild",
                CICIUseType.DESTROY => "Destroy",
                CICIUseType.USE => "Use",
                CICIUseType.CONSUME => "Consume",
                _ => throw new UnreachableException(),
            };

            string alternativeUseTypeString = item.AlternativeUseType switch
            {
                CICIUseType.NONE => "None",

                CICIUseType.BUILD => "Build",
                CICIUseType.BUILD_ATTACK => "BuildAttack",
                CICIUseType.INTERACT => "Interact",
                CICIUseType.INTERACT_AND_BUILD => "InteractAndBuild",
                CICIUseType.DESTROY => "Destroy",
                CICIUseType.USE => "Use",
                CICIUseType.CONSUME => "Consume",
                _ => throw new UnreachableException(),
            };

            int health;
            if (item.BlockInfo is not null)
            {
                health = item.BlockInfo.BreakingHealth;
            }
            else if (item.ToolInfo is not null)
            {
                health = item.ToolInfo.MaxWear;
            }
            else if (item.MobInfo is not null)
            {
                health = item.MobInfo.Health;
            }
            else
            {
                health = 0;
            }

            int blockDamage;
            if (item.ToolInfo is not null)
            {
                blockDamage = item.ToolInfo.BlockDamage;
            }
            else
            {
                blockDamage = 0;
            }

            int mobDamage;
            if (item.ToolInfo is not null)
            {
                mobDamage = item.ToolInfo.MobDamage;
            }
            else if (item.ProjectileInfo is not null)
            {
                mobDamage = item.ProjectileInfo.MobDamage;
            }
            else
            {
                mobDamage = 0;
            }

            ItemsCatalog.ItemR.ItemData.BlockMetadataR? blockMetadata;
            if (item.BlockInfo is not null)
            {
                blockMetadata = new ItemsCatalog.ItemR.ItemData.BlockMetadataR(item.BlockInfo.BreakingHealth, item.BlockInfo.EfficiencyCategory);
            }
            else if (item.MobInfo is not null)
            {
                blockMetadata = new ItemsCatalog.ItemR.ItemData.BlockMetadataR(item.MobInfo.Health, "instant");
            }
            else
            {
                blockMetadata = null;
            }

            BoostMetadata? boostMetadata;
            if (item.BoostInfo is not null)
            {
                string boostTypeString = item.BoostInfo.Type switch
                {
                    CICIBIType.POTION => "Potion",
                    CICIBIType.INVENTORY_ITEM => "InventoryItem",
                    _ => throw new UnreachableException(),
                };

                string boostAttributeString = item.BoostInfo.Effects[0].Type switch
                {
                    CICIBIEType.ADVENTURE_XP => "ItemExperiencePoints",
                    CICIBIEType.CRAFTING => "Crafting",
                    CICIBIEType.DEFENSE => "Defense",
                    CICIBIEType.EATING => "Eating",
                    CICIBIEType.HEALING => "Healing",
                    CICIBIEType.HEALTH => "MaximumPlayerHealth",
                    CICIBIEType.ITEM_XP => "ItemExperiencePoints",
                    CICIBIEType.MINING_SPEED => "MiningSpeed",
                    CICIBIEType.RETENTION_BACKPACK or CICIBIEType.RETENTION_HOTBAR or CICIBIEType.RETENTION_XP => "Retention",
                    CICIBIEType.SMELTING => "Smelting",
                    CICIBIEType.STRENGTH => "Strength",
                    CICIBIEType.TAPPABLE_RADIUS => "TappableInteractionRadius",
                    _ => throw new UnreachableException(),
                };

                boostMetadata = new BoostMetadata(
                    item.BoostInfo.Name,
                    boostTypeString,
                    boostAttributeString,
                    false,
                    item.BoostInfo.CanBeRemoved,
                    TimeFormatter.FormatDuration(item.BoostInfo.Duration),
                    true,
                    item.BoostInfo.Level,
                    [.. item.BoostInfo.Effects.Select(effect => BoostUtils.BoostEffectToApiResponse(effect, item.BoostInfo.Duration))],
                    item.BoostInfo.TriggeredOnDeath ? "Death" : null,
                    null
                );
            }
            else
            {
                boostMetadata = null;
            }

            ItemsCatalog.ItemR.ItemData.JournalMetadataR? journalMetadata;
            if (item.JournalEntry is not null)
            {
                string behaviorString = item.JournalEntry.Behavior switch
                {
                    CICIJEBehavior.NONE => "None",
                    CICIJEBehavior.PASSIVE => "Passive",
                    CICIJEBehavior.HOSTILE => "Hostile",
                    CICIJEBehavior.NEUTRAL => "Neutral",
                    _ => throw new UnreachableException(),
                };

                string biomeString = item.JournalEntry.Biome switch
                {
                    CICIJEBiome.NONE => "None",
                    CICIJEBiome.OVERWORLD => "Overworld",
                    CICIJEBiome.NETHER => "Hell",
                    CICIJEBiome.BIRCH_FOREST => "BirchForest",
                    CICIJEBiome.DESERT => "Desert",
                    CICIJEBiome.FLOWER_FOREST => "FlowerForest",
                    CICIJEBiome.FOREST => "Forest",
                    CICIJEBiome.ICE_PLAINS => "IcePlains",
                    CICIJEBiome.JUNGLE => "Jungle",
                    CICIJEBiome.MESA => "Mesa",
                    CICIJEBiome.MUSHROOM_ISLAND => "MushroomIsland",
                    CICIJEBiome.OCEAN => "Ocean",
                    CICIJEBiome.PLAINS => "Plains",
                    CICIJEBiome.RIVER => "River",
                    CICIJEBiome.ROOFED_FOREST => "RoofedForest",
                    CICIJEBiome.SAVANNA => "Savanna",
                    CICIJEBiome.SUNFLOWER_PLAINS => "SunFlowerPlains",
                    CICIJEBiome.SWAMP => "Swampland",
                    CICIJEBiome.TAIGA => "Taiga",
                    CICIJEBiome.WARM_OCEAN => "WarmOcean",
                    _ => throw new UnreachableException(),
                };

                journalMetadata = new ItemsCatalog.ItemR.ItemData.JournalMetadataR(
                    item.JournalEntry.Group,
                    item.Experience.Journal,
                    item.JournalEntry.Order,
                    behaviorString,
                    biomeString
                );
            }
            else
            {
                journalMetadata = null;
            }

            return new ItemsCatalog.ItemR(
                item.Id,
                new ItemsCatalog.ItemR.ItemData(
                    item.Name,
                    item.Aux,
                    typeString,
                    useTypeString,
                    0,
                    item.ConsumeInfo?.Heal,
                    0,
                    mobDamage,
                    blockDamage,
                    health,
                    blockMetadata,
                    new ItemsCatalog.ItemR.ItemData.ItemMetadataR(
                        useTypeString,
                        alternativeUseTypeString,
                        mobDamage,
                        blockDamage,
                        null,
                        0,
                        item.ConsumeInfo is not null ? item.ConsumeInfo.Heal : 0,
                        item.ToolInfo?.EfficiencyCategory,
                        health
                    ),
                    boostMetadata,
                    journalMetadata,
                    item.JournalEntry is not null && item.JournalEntry.Sound is not null ? new ItemsCatalog.ItemR.ItemData.AudioMetadataR(
                        new Dictionary<string, string>() { ["journal"] = item.JournalEntry.Sound },
                        item.JournalEntry.Sound
                    ) : null,
                    new Dictionary<string, object>()
                ),
                categoryString,
                Types.Common.Rarity.FromStaticData(item.Rarity),
                1,
                item.Stackable,
                item.FuelInfo is not null ? new Types.Common.BurnRate(item.FuelInfo.BurnTime, item.FuelInfo.HeatPerSecond) : null,
                item.FuelInfo is not null && item.FuelInfo.ReturnItemId is not null ? [new ItemsCatalog.ItemR.ReturnItem(item.FuelInfo.ReturnItemId, 1)] : [],
                item.ConsumeInfo is not null && item.ConsumeInfo.ReturnItemId is not null ? [new ItemsCatalog.ItemR.ReturnItem(item.ConsumeInfo.ReturnItemId, 1)] : [],
                item.Experience.Tappable,
                new Dictionary<string, int?>() { ["tappable"] = item.Experience.Tappable, ["encounter"] = item.Experience.Encounter, ["crafting"] = item.Experience.Crafting },
                false
            );
        })];

        Dictionary<string, ItemsCatalog.EfficiencyCategory> efficiencyCategories = [];
        foreach (Catalog.ItemEfficiencyCategoriesCatalogR.EfficiencyCategory efficiencyCategory in catalog.ItemEfficiencyCategoriesCatalog.EfficiencyCategories)
        {
            efficiencyCategories[efficiencyCategory.Name] = new ItemsCatalog.EfficiencyCategory(
                new ItemsCatalog.EfficiencyCategory.EfficiencyMapR(
                    efficiencyCategory.Hand,
                    efficiencyCategory.Hoe,
                    efficiencyCategory.Axe,
                    efficiencyCategory.Shovel,
                    efficiencyCategory.Pickaxe_1,
                    efficiencyCategory.Pickaxe_2,
                    efficiencyCategory.Pickaxe_3,
                    efficiencyCategory.Pickaxe_4,
                    efficiencyCategory.Pickaxe_5,
                    efficiencyCategory.Sword,
                    efficiencyCategory.Sheers
                )
            );
        }

        return new ItemsCatalog(items, efficiencyCategories);
    }

    private static RecipesCatalog MakeRecipesCatalogApiResponse(Catalog catalog)
    {
        RecipesCatalog.CraftingRecipe[] crafting = [.. catalog.RecipesCatalog.Crafting.Select(recipe =>
        {
            string categoryString = recipe.Category switch
            {
                CRCCRCategory.CONSTRUCTION => "Construction",
                CRCCRCategory.EQUIPMENT => "Equipment",
                CRCCRCategory.ITEMS => "Items",
                CRCCRCategory.NATURE => "Nature",
                _ => throw new UnreachableException(),
            };

            return new RecipesCatalog.CraftingRecipe(
                    recipe.Id,
                    categoryString,
                    TimeFormatter.FormatDuration(recipe.Duration * 1000),
                    [.. recipe.Ingredients.Select(ingredient => new RecipesCatalog.CraftingRecipe.Ingredient(ingredient.PossibleItemIds, ingredient.Count))],
                    new RecipesCatalog.CraftingRecipe.OutputR(recipe.Output.ItemId, recipe.Output.Count),
                    [.. recipe.ReturnItems.Select(returnItem => new RecipesCatalog.CraftingRecipe.ReturnItem(returnItem.ItemId, returnItem.Count))],
                    false
            );
        })];

        RecipesCatalog.SmeltingRecipe[] smelting = [.. catalog.RecipesCatalog.Smelting.Select(recipe =>
        {
            return new RecipesCatalog.SmeltingRecipe(
                recipe.Id,
                recipe.HeatRequired,
                recipe.Input,
                new RecipesCatalog.SmeltingRecipe.OutputR(recipe.Output, 1),
                recipe.ReturnItemId is not null ? [new RecipesCatalog.SmeltingRecipe.ReturnItem(recipe.ReturnItemId, 1)] : [],
                false
            );
        })];

        return new RecipesCatalog(crafting, smelting);
    }

    private static JournalCatalog MakeJournalCatalogApiResponse(Catalog catalog)
    {
        Dictionary<string, JournalCatalog.Item> items = [];
        foreach (Catalog.ItemJournalGroupsCatalogR.JournalGroup group in catalog.ItemJournalGroupsCatalog.Groups)
        {
            string parentCollectionString = group.ParentCollection switch
            {
                CIJGCJGParentCollection.BLOCKS => "Blocks",
                CIJGCJGParentCollection.ITEMS_CRAFTED => "ItemsCrafted",
                CIJGCJGParentCollection.ITEMS_SMELTED => "ItemsSmelted",
                CIJGCJGParentCollection.MOBS => "Mobs",
                _ => throw new UnreachableException(),
            };

            items[group.Name] = new JournalCatalog.Item(
                    group.Id,
                    parentCollectionString,
                    group.Order,
                    group.Order,
                    group.DefaultSound,
                    false,
                    "200526.173531"
            );
        }

        return new JournalCatalog(items);
    }

    private static NFCBoost[] MakeNFCBoostsCatalogApiResponse(Catalog catalog)
        => [.. catalog.NfcBoostsCatalog.MiniFigs.Select(miniFig => new NFCBoost(
            miniFig.Id,
            miniFig.Name,
            "NfcMiniFig",
            new Types.Common.Rewards(
                miniFig.Rewards.Rubies,
                miniFig.Rewards.ExperiencePoints,
                miniFig.Rewards.Level,
                [.. (miniFig.Rewards.Inventory ?? []).Select(item => new Types.Common.Rewards.Item(item.Id, item.Amount))],
                miniFig.Rewards.Buildplates ?? [],
                [.. (miniFig.Rewards.Challenges ?? []).Select(challenge => new Types.Common.Rewards.Challenge(challenge.Id))],
                miniFig.Rewards.PersonaItems ?? [],
                [.. (miniFig.Rewards.UtilityBlocks ?? []).Select(_ => new Types.Common.Rewards.UtilityBlock())]
            ),
            new BoostMetadata(
                miniFig.BoostMetadata.Name,
                "MiniFig",
                miniFig.BoostMetadata.Attribute,
                miniFig.BoostMetadata.CanBeDeactivated,
                miniFig.BoostMetadata.CanBeRemoved,
                miniFig.BoostMetadata.ActiveDuration,
                miniFig.BoostMetadata.Additive,
                miniFig.BoostMetadata.Level,
                [.. miniFig.BoostMetadata.Effects.Select(effect => new Effect(
                    effect.Type,
                    effect.Duration,
                    effect.Value is null ? null : (int)Math.Round(effect.Value.Value),
                    effect.Unit,
                    effect.Targets,
                    effect.Items,
                    effect.ItemScenarios,
                    effect.Activation,
                    effect.ModifiesType
                ))],
                miniFig.BoostMetadata.Scenario,
                miniFig.BoostMetadata.Cooldown
            ),
            miniFig.Deprecated,
            miniFig.ToolsVersion
        ))];

    private HashSet<string> ProductIdsFromQuery()
    {
        var productIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (string key in new[] { "productId", "id", "productIds", "recentlyViewedProductIds", "ids" })
        {
            foreach (string? value in Request.Query[key])
            {
                if (value is null)
                {
                    continue;
                }

                foreach (string productId in value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    productIds.Add(productId);
                }
            }
        }

        return productIds;
    }

    private static async Task<HashSet<string>> ReadRequestedProductIdsAsync(Stream body, CancellationToken cancellationToken)
    {
        var productIds = new HashSet<string>(StringComparer.Ordinal);

        try
        {
            using JsonDocument document = await JsonDocument.ParseAsync(body, cancellationToken: cancellationToken);
            AddProductIds(document.RootElement, productIds);
        }
        catch (JsonException)
        {
        }

        return productIds;
    }

    private static void AddProductIds(JsonElement element, HashSet<string> productIds)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (JsonProperty property in element.EnumerateObject())
                {
                    if (property.Name.Equals("productId", StringComparison.OrdinalIgnoreCase)
                        || property.Name.Equals("id", StringComparison.OrdinalIgnoreCase)
                        || property.Name.Equals("productIds", StringComparison.OrdinalIgnoreCase)
                        || property.Name.Equals("recentlyViewedProductIds", StringComparison.OrdinalIgnoreCase)
                        || property.Name.Equals("ids", StringComparison.OrdinalIgnoreCase))
                    {
                        AddProductIds(property.Value, productIds);
                    }
                    else if (property.Value.ValueKind is JsonValueKind.Object or JsonValueKind.Array)
                    {
                        AddProductIds(property.Value, productIds);
                    }
                }

                break;
            case JsonValueKind.Array:
                foreach (JsonElement item in element.EnumerateArray())
                {
                    AddProductIds(item, productIds);
                }

                break;
            case JsonValueKind.String:
                string? productId = element.GetString();
                if (!string.IsNullOrWhiteSpace(productId))
                {
                    productIds.Add(productId);
                }

                break;
        }
    }
}
