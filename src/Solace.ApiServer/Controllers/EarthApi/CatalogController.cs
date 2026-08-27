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
    private readonly CatalogResponseCacheService _responseCache;

    public CatalogController(StaticData.StaticData staticData, CatalogResponseCacheService responseCache)
    {
        _catalog = staticData.Catalog;
        _responseCache = responseCache;
    }

    [HttpGet("inventory/catalogv3")]
    public ContentHttpResult GetItemsCatalog()
        => EarthJson(_responseCache.GetItemsCatalog());

    [HttpGet("recipes")]
    public ContentHttpResult GetRecipeCatalog()
        => EarthJson(_responseCache.GetRecipeCatalog());

    [HttpGet("journal/catalog")]
    public ContentHttpResult GetJournalCatalog()
        => EarthJson(_responseCache.GetJournalCatalog());

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
