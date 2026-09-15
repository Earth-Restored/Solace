using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Solace.ApiServer.Types.Catalog;
using Solace.StaticData;
using Solace.ApiServer.Types.Common;
using Solace.ApiServer.Utils;

namespace Solace.ApiServer.Controllers;

[Authorize]
[ApiVersion("1.1")]
[Route("1/api/v{version:apiVersion}")]
internal sealed class CatalogController : SolaceControllerBase
{
    private readonly Catalog _catalog;
    private readonly CatalogResponseCacheService _responseCache;

    public CatalogController(StaticDataProvider staticData, CatalogResponseCacheService responseCache)
    {
        _catalog = staticData.Catalog;
        _responseCache = responseCache;
    }

    [HttpGet("inventory/catalogv3")]
    public EarthApiResponse<ItemsCatalog> GetItemsCatalog()
        => new(_responseCache.GetItemsCatalog());

    [HttpGet("recipes")]
    public EarthApiResponse<RecipesCatalog> GetRecipeCatalog()
        => new(_responseCache.GetRecipeCatalog());

    [HttpGet("journal/catalog")]
    public EarthApiResponse<JournalCatalog> GetJournalCatalog()
        => new(_responseCache.GetJournalCatalog());

    [HttpGet("products/catalog")]
    public EarthApiResponse<NFCBoost[]> GetNFCBoostsCatalog()
        => new(MakeNFCBoostsCatalogApiResponse(_catalog));

    private static NFCBoost[] MakeNFCBoostsCatalogApiResponse(Catalog catalog)
        => [.. catalog.NfcBoostsCatalog.MiniFigs.Values.Select(miniFig => new NFCBoost(
            miniFig.Id,
            miniFig.Name,
            "NfcMiniFig",
            new Types.Common.Rewards(
                miniFig.Rewards.Rubies,
                miniFig.Rewards.ExperiencePoints,
                miniFig.Rewards.Level,
                [.. (miniFig.Rewards.Inventory ?? []).Select(item => new Types.Common.RewardsItem(item.Id, item.Amount))],
                miniFig.Rewards.Buildplates ?? [],
                [.. (miniFig.Rewards.Challenges ?? []).Select(challenge => new Types.Common.RewardsChallenge(challenge.Id))],
                miniFig.Rewards.PersonaItems ?? [],
                [.. (miniFig.Rewards.UtilityBlocks ?? []).Select(_ => new Types.Common.RewardsUtilityBlock())]
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
