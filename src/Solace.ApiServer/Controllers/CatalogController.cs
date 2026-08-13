using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Solace.ApiServer.Types.Catalog;
using Solace.StaticData;
using Solace.ApiServer.Types.Common;

namespace Solace.ApiServer.Controllers;

[Authorize]
[ApiVersion("1.1")]
[Route("1/api/v{version:apiVersion}")]
internal sealed class CatalogController : SolaceControllerBase
{
    private readonly Catalog _catalog;
    private readonly CatalogResponseCacheService _responseCache;

    public CatalogController(StaticData.StaticDataProvider staticData, CatalogResponseCacheService responseCache)
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

    private static NFCBoost[] MakeNFCBoostsCatalogApiResponse(Catalog catalog)
        => [.. catalog.NfcBoostsCatalog.MiniFigs.Values.Select(miniFig => new NFCBoost(
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
                    effect.Value is null ? null : (int)double.Round(effect.Value.Value),
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
}
