using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Solace.ApiServer.Types.Common;
using Solace.ApiServer.Types.Tappables;
using Solace.ApiServer.Utils;
using Solace.Common.Utils;
using Solace.Db.Earth;
using Solace.Db.Earth.Models.Player;
using Solace.StaticData;
using Microsoft.EntityFrameworkCore;
using Solace.ObjectStore.Client;

namespace Solace.ApiServer.Controllers;

[Authorize]
[ApiVersion("1.1")]
[Route("1/api/v{version:apiVersion}")]
internal sealed class TappablesController : SolaceControllerBase
{
    private readonly TappablesManager _tappablesManager;
    private readonly EarthDbContext _earthDb;
    private readonly ObjectStoreClient _objectStore;
    private readonly StaticDataProvider _staticData;

    public TappablesController(TappablesManager tappablesManager, EarthDbContext earthDb, ObjectStoreClient objectStore, StaticDataProvider staticData)
    {
        _tappablesManager = tappablesManager;
        _earthDb = earthDb;
        _objectStore = objectStore;
        _staticData = staticData;
    }

    [HttpGet("locations/{lat}/{lon}")]
    public async Task<Results<ContentHttpResult, BadRequest>> GetTappables(double lat, double lon, CancellationToken cancellationToken)
    {
        if (!TryGetProfileId(out var accountId))
        {
            return TypedResults.BadRequest();
        }

        var requestStartedOn = HttpContext.GetTimestamp();

        await _tappablesManager.NotifyTileActiveAsync(accountId, lat, lon, cancellationToken);

        var tappables = _tappablesManager.GetTappablesAround(lat, lon, 5.0);    // TODO: radius
        var encounters = _tappablesManager.GetEncountersAround(lat, lon, 5.0);    // TODO: radius

        var redeemedTappables = await _earthDb.RedeemedTappables
            .AsNoTracking()
            .Where(rt => rt.ProfileId == accountId)
            .Select(rt => rt.TappableId)
            .ToHashSetAsync(cancellationToken);

        var activeLocationTappables = tappables
            .Where(tappable => tappable.SpawnTime + tappable.ValidFor > requestStartedOn && !redeemedTappables.Contains(tappable.Id))
            .Select(tappable => new ActiveLocation(
                tappable.Id,
                TappablesManager.LocationToTileIdString(tappable.Lat, tappable.Lon),
                new Coordinate(tappable.Lat, tappable.Lon),
                TimeFormatter.FormatTime(tappable.SpawnTime),
                TimeFormatter.FormatTime(tappable.SpawnTime + tappable.ValidFor),
                ActiveLocation.TypeE.TAPPABLE,
                tappable.Icon,
                new ActiveLocation.MetadataR(Guid.NewGuid(), Rarity.FromTappable(tappable.Rarity)),
                new ActiveLocation.TappableMetadataR(Rarity.FromTappable(tappable.Rarity)),
                null
            ));

        var activeLocationEncounters = encounters
            .Where(encounter => encounter.SpawnTime + encounter.ValidFor > requestStartedOn)
            .Select(encounter => new ActiveLocation(
                encounter.Id,
                TappablesManager.LocationToTileIdString(encounter.Lat, encounter.Lon),
                new Coordinate(encounter.Lat, encounter.Lon),
                TimeFormatter.FormatTime(encounter.SpawnTime),
                TimeFormatter.FormatTime(encounter.SpawnTime + encounter.ValidFor),
                ActiveLocation.TypeE.ENCOUNTER,
                encounter.Icon,
                new ActiveLocation.MetadataR(Guid.NewGuid(), Rarity.FromEncounter(encounter.Rarity)),
                null,
                new ActiveLocation.EncounterMetadataR(
                    ActiveLocation.EncounterMetadataR.EncounterTypeE.SHORT_4X4_PEACEFUL,    // TODO
                                                                                            //UUID.randomUUID().toString(),    // TODO: what is this field for and does it matter what we put here?
                    encounter.Id,
                    encounter.EncounterBuildplateId,
                    ActiveLocation.EncounterMetadataR.AnchorStateE.OFF,
                    "",
                    ""
                )
            ));

        ActiveLocation[] activeLocations = [.. activeLocationTappables, .. activeLocationEncounters];

        return EarthJson(new Dictionary<string, object>(StringComparer.Ordinal)
        {
            { "killSwitchedTileIds", new List<object>() },
            { "activeLocations", activeLocations }
        });
    }

    [HttpPost("tappables/{tileIdStr}")]
    public async Task<Results<ContentHttpResult, BadRequest>> RedeemTappable(string tileIdStr, CancellationToken cancellationToken)
    {
        if (!TryGetProfileId(out var accountId))
        {
            return TypedResults.BadRequest();
        }

        var tappableRequest = await Request.Body.AsJsonAsync(AppJsonContext.Default.TappableRequest, cancellationToken);
        if (tappableRequest is null)
        {
            return TypedResults.BadRequest();
        }

        // request.timestamp
        var requestStartedOn = HttpContext.GetTimestamp();

        if (!TappablesManager.TryParseTileId(tileIdStr, out var tileId))
        {
            return TypedResults.BadRequest();
        }

        var tappable = _tappablesManager.GetTappableWithId(tappableRequest.Id, tileId);
        if (tappable is null || !TappablesManager.IsTappableValidFor(tappable, requestStartedOn, tappableRequest.PlayerCoordinate.Latitude, tappableRequest.PlayerCoordinate.Longitude))
        {
            return TypedResults.BadRequest();
        }

        var boosts = await _earthDb.Boosts
            .AsNoTracking()
            .FirstAsync(boosts => boosts.Id == accountId, cancellationToken: cancellationToken);

        if (await _earthDb.RedeemedTappables.AnyAsync(rd => rd.ProfileId == accountId && rd.TappableId == tappable.Id, cancellationToken))
        {
            return TypedResults.BadRequest();
        }

        var experiencePointsGlobalMultiplier = 0;

        Dictionary<Guid, int> experiencePointsPerItemMultiplier = [];
        foreach (var effect in Common.Utils.BoostUtils.GetActiveEffects(boosts, requestStartedOn, _staticData.Catalog))
        {
            if (effect.Type is Catalog.ItemsCatalogR.Item.BoostEffectType.ITEM_XP)
            {
                if (effect.ApplicableItemIds is not null && effect.ApplicableItemIds.Length > 0)
                {
                    foreach (var itemId in effect.ApplicableItemIds)
                    {
                        experiencePointsPerItemMultiplier[itemId] = experiencePointsPerItemMultiplier.GetValueOrDefault(itemId) + effect.Value;
                    }
                }
                else
                {
                    experiencePointsGlobalMultiplier += effect.Value;
                }
            }
        }

        var rewards = new Utils.Rewards();

        foreach (var item in tappable.Items)
        {
            rewards.AddItem(item.Id, item.Count);
            var experiencePoints = _staticData.Catalog.ItemsCatalog.GetItem(item.Id)!.Experience.Tappable;
            var experiencePointsMultiplier = experiencePointsGlobalMultiplier + experiencePointsPerItemMultiplier.GetValueOrDefault(item.Id);
            if (experiencePointsMultiplier > 0)
            {
                experiencePoints = experiencePoints * (experiencePointsMultiplier + 100) / 100;
            }

            rewards.AddExperiencePoints(experiencePoints * item.Count);
        }

        rewards.AddRubies(1); // TODO

        _earthDb.RedeemedTappables.Add(new RedeemedTappableEF { ProfileId = accountId, TappableId = tappable.Id, ExpiresAt = tappable.SpawnTime + tappable.ValidFor, });
        await _earthDb.SaveChangesAsync(cancellationToken);

        await RedeemedTappableUtils.PruneAsync(_earthDb, requestStartedOn, cancellationToken);

        await _earthDb.SaveChangesAsync(cancellationToken);
        var results = new ResultsEF.Builder();

        await ActivityLogUtils.AddEntryAsync(_earthDb, results, accountId, new TappableEntryEF(accountId, requestStartedOn, rewards.ToDBRewardsModel()), cancellationToken);
        await rewards.ToRedeemQueryAsync(_earthDb, results, _objectStore, accountId, requestStartedOn, _staticData, cancellationToken);

        return EarthJson(new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            { "token", new Token(
                Token.Type.TAPPABLE,
                [with(StringComparer.Ordinal)],
                rewards.ToApiResponse(),
                Token.LifetimeE.PERSISTENT
            ) },
            { "updates", null }
        }, new EarthApiResponse.UpdatesResponse(await results.BuildAsync(_earthDb, accountId, cancellationToken)));
    }

    [HttpPost("multiplayer/encounters/state")]
    [HttpPost("multiplayer/adventures/state")]
    [HttpPost("multiplayer/player/adventures/state")]
    public async Task<Results<ContentHttpResult, BadRequest>> EncountersState(CancellationToken cancellationToken)
    {
        var requestedIds = await Request.Body.AsJsonAsync(AppJsonContext.Default.DictionaryStringObject, cancellationToken);

        if (requestedIds is null)
        {
            return TypedResults.BadRequest();
        }

        foreach (var entry in requestedIds)
        {
            if (entry.Value is not string)
            {
                return TypedResults.BadRequest();
            }
        }

        // TODO

        var encounterStates = new Dictionary<string, EncounterState>(StringComparer.Ordinal);
#pragma warning disable IDE0059 // Unnecessary assignment of a value
        foreach (var (encounterId, tileId) in requestedIds)
        {
            encounterStates[encounterId] = new EncounterState(EncounterState.ActiveEncounterStateE.PRISTINE);
        }
#pragma warning restore IDE0059 // Unnecessary assignment of a value

        return EarthJson(encounterStates);
    }

    internal sealed record TappableRequest(
        Guid Id,
        Coordinate PlayerCoordinate
    );
}
