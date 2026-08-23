using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using Solace.ApiServer.Utils;
using Solace.Db.Earth;
using Solace.Db.Earth.Models.Player;
using Solace.StaticData;
using Effect = Solace.ApiServer.Types.Common.Effect;
using Microsoft.EntityFrameworkCore;
using Solace.Common;
using Solace.Db.Earth.Models;

namespace Solace.ApiServer.Controllers;

[Authorize]
[ApiVersion("1.1")]
[Route("1/api/v{version:apiVersion}")]
internal sealed partial class BoostsController : SolaceControllerBase
{
    private readonly EarthDbContext _earthDb;
    private readonly Catalog _catalog;
    private readonly ILogger<BoostsController> _logger;

    public BoostsController(EarthDbContext earthDB, StaticData.StaticDataProvider staticData, ILogger<BoostsController> logger)
    {
        _earthDb = earthDB;
        _catalog = staticData.Catalog;
        _logger = logger;
    }

    private sealed record ActiveBoostInfo(
        BoostsEF.ActiveBoost ActiveBoost,
        Catalog.ItemsCatalogR.Item.BoostInfoR BoostInfo
    );

    [HttpGet("boosts")]
    public async Task<Results<ContentHttpResult, BadRequest>> GetBoosts(CancellationToken cancellationToken)
    {
        if (!TryGetProfileId(out var accountId))
        {
            return TypedResults.BadRequest();
        }

        var requestStartedOn = HttpContext.GetTimestamp();

        // I know this is ugly, we're making changes to the database in response to a GET request, but if we don't then the client won't correctly update the player health bar in the UI

        var boosts = await _earthDb.Boosts
            .AsNoTracking()
            .FirstAsync(boosts => boosts.Id == accountId, cancellationToken: cancellationToken);

        var profile = await _earthDb.Profiles
            .AsTracking()
            .FirstAsync(profile => profile.Id == accountId, cancellationToken: cancellationToken);

        var results = new ResultsEF.Builder();

        if (PruneBoostsAndUpdateProfile(boosts, profile, requestStartedOn, _catalog))
        {
            await _earthDb.SaveChangesAsync(cancellationToken);
            results.Profile();
        }

        var potions = new Types.Boost.Boosts.Potion?[5];
        var miniFigs = new Types.Boost.Boosts.MiniFig?[5];

        for (var index = 0; index < boosts.ActiveBoosts.Length && index < 5; index++)
        {
            var activeBoost = boosts.ActiveBoosts[index];
            if (activeBoost is null)
            {
                continue;
            }

            var item = _catalog.ItemsCatalog.GetItem(activeBoost.ItemId);
            if (item is not null && item.BoostInfo is not null)
            {
                potions[index] = new Types.Boost.Boosts.Potion(
                    true,
                    activeBoost.ItemId,
                    activeBoost.InstanceId,
                    TimeFormatter.FormatTime(activeBoost.StartTime + activeBoost.Duration)
                );
            }
            else
            {
                var nfcBoost = _catalog.NfcBoostsCatalog.MiniFigs.Values.FirstOrDefault(m => IdTranslator.ToGuid(m.Id) == activeBoost.ItemId);
                if (nfcBoost is not null)
                {
                    miniFigs[index] = new Types.Boost.Boosts.MiniFig(
                        true,
                        nfcBoost.Id,
                        activeBoost.InstanceId,
                        TimeFormatter.FormatTime(activeBoost.StartTime + activeBoost.Duration)
                    );
                }
            }
        }

        Dictionary<string, ActiveBoostInfo> activeBoostsWithInfo = [with(StringComparer.Ordinal)];
        foreach (var activeBoost in boosts.ActiveBoosts)
        {
            if (activeBoost is null)
            {
                continue;
            }

            var item = _catalog.ItemsCatalog.GetItem(activeBoost.ItemId);
            if (item is null || item.BoostInfo is null)
            {
                continue;
            }

            var existingActiveBoostInfo = activeBoostsWithInfo.GetValueOrDefault(item.BoostInfo.Name);
            if (existingActiveBoostInfo is not null && existingActiveBoostInfo.BoostInfo.Level > item.BoostInfo.Level)
            {
                continue;
            }

            activeBoostsWithInfo[item.BoostInfo.Name] = new ActiveBoostInfo(activeBoost, item.BoostInfo);
        }

        LinkedList<Types.Boost.Boosts.ActiveEffect> activeEffects = [];
        LinkedList<Types.Boost.Boosts.ScenarioBoost> triggeredOnDeathBoosts = [];
        foreach (var activeBoostInfo in activeBoostsWithInfo.Values)
        {
            if (!activeBoostInfo.BoostInfo.TriggeredOnDeath)
            {
                foreach (var effect in activeBoostInfo.BoostInfo.Effects)
                {
                    if (effect.Activation != Catalog.ItemsCatalogR.Item.BoostEffectActivation.TIMED)
                    {
                        LogUnexpectedBoostActivation(activeBoostInfo.ActiveBoost.ItemId, effect.Activation);
                        continue;
                    }

                    activeEffects.AddLast(new Types.Boost.Boosts.ActiveEffect(BoostUtils.BoostEffectToApiResponse(effect, activeBoostInfo.ActiveBoost.Duration), TimeFormatter.FormatTime(activeBoostInfo.ActiveBoost.StartTime + activeBoostInfo.ActiveBoost.Duration)));
                }
            }
            else
            {
                var effects = new List<Effect>(activeBoostInfo.BoostInfo.Effects.Length);
                foreach (var effect in activeBoostInfo.BoostInfo.Effects)
                {
                    if (effect.Activation is not Catalog.ItemsCatalogR.Item.BoostEffectActivation.TRIGGERED)
                    {
                        LogUnexpectedBoostActivation(activeBoostInfo.ActiveBoost.ItemId, effect.Activation);
                        continue;
                    }

                    effects.Add(BoostUtils.BoostEffectToApiResponse(effect, activeBoostInfo.ActiveBoost.Duration));
                }

                triggeredOnDeathBoosts.AddLast(new Types.Boost.Boosts.ScenarioBoost(true, activeBoostInfo.ActiveBoost.InstanceId, [.. effects], TimeFormatter.FormatTime(activeBoostInfo.ActiveBoost.StartTime + activeBoostInfo.ActiveBoost.Duration)));
            }
        }

        Dictionary<string, Types.Boost.Boosts.ScenarioBoost[]> scenarioBoosts = [with(StringComparer.Ordinal)];
        if (triggeredOnDeathBoosts.Count > 0)
        {
            scenarioBoosts["death"] = [.. triggeredOnDeathBoosts];
        }

        var statModiferValues = Common.Utils.BoostUtils.GetActiveStatModifiers(boosts, requestStartedOn, _catalog);

        var boostsResponse = new Types.Boost.Boosts(
            potions,
            miniFigs,
            [.. activeEffects],
            scenarioBoosts,
            new Types.Boost.Boosts.StatusEffectsR(
                statModiferValues.TappableInteractionRadiusExtraMeters > 0 ? statModiferValues.TappableInteractionRadiusExtraMeters + 70 : null,
                null,
                null,
                statModiferValues.AttackMultiplier > 0 ? statModiferValues.AttackMultiplier + 100 : null,
                statModiferValues.DefenseMultiplier > 0 ? statModiferValues.DefenseMultiplier + 100 : null,
                statModiferValues.MiningSpeedMultiplier > 0 ? statModiferValues.MiningSpeedMultiplier + 100 : null,
                statModiferValues.MaxPlayerHealthMultiplier > 0 ? 20 * statModiferValues.MaxPlayerHealthMultiplier / 100 + 20 : 20,
                statModiferValues.CraftingSpeedMultiplier > 0 ? statModiferValues.CraftingSpeedMultiplier / 100 + 1 : null,
                statModiferValues.SmeltingSpeedMultiplier > 0 ? statModiferValues.SmeltingSpeedMultiplier / 100 + 1 : null,
                statModiferValues.FoodMultiplier > 0 ? (statModiferValues.FoodMultiplier + 100) / 100f : null
            ),
            [with(StringComparer.Ordinal)],
            activeBoostsWithInfo.Count != 0 ? TimeFormatter.FormatTime(activeBoostsWithInfo.Values.Min(activeBoostInfo => activeBoostInfo.ActiveBoost.StartTime + activeBoostInfo.ActiveBoost.Duration)) : null
        );

        return EarthJson(boostsResponse, new EarthApiResponse.UpdatesResponse(await results.BuildAsync(_earthDb, accountId, cancellationToken)));
    }

    [HttpPost("boosts/potions/{itemId}/activate")]
    public async Task<Results<ContentHttpResult, BadRequest>> ActivateBoost(Guid itemId, CancellationToken cancellationToken)
    {
        if (!TryGetProfileId(out var accountId))
        {
            return TypedResults.BadRequest();
        }

        var requestStartedOn = HttpContext.GetTimestamp();

        var item = _catalog.ItemsCatalog.GetItem(itemId);

        if (item is null || item.BoostInfo is null || item.BoostInfo.Type is not Catalog.ItemsCatalogR.Item.BoostInfoType.POTION)
        {
            return TypedResults.BadRequest();
        }

        var boosts = await _earthDb.Boosts
            .AsTracking()
            .FirstAsync(boosts => boosts.Id == accountId, cancellationToken: cancellationToken);

        var profile = await _earthDb.Profiles
            .AsTracking()
            .FirstAsync(profile => profile.Id == accountId, cancellationToken: cancellationToken);

        var profileChanged = false;

        if (PruneBoostsAndUpdateProfile(boosts, profile, requestStartedOn, _catalog))
        {
            profileChanged = true;
        }

        var results = new ResultsEF.Builder();

        if (!await InventoryUtils.TakeStackableItemsAsync(_earthDb, results, accountId, itemId, 1, cancellationToken))
        {
            return EarthJson(null, null);
        }

        var newIndex = -1;
        var extendExisting = false;
        for (var index = 0; index < boosts.ActiveBoosts.Length; index++)
        {
            var boost = boosts.ActiveBoosts[index];

            if (boost is not null && boost.ItemId == itemId)
            {
                newIndex = index;
                extendExisting = true;
                break;
            }
        }

        if (!extendExisting)
        {
            for (var index = 0; index < boosts.ActiveBoosts.Length; index++)
            {
                if (boosts.ActiveBoosts[index] is null)
                {
                    newIndex = index;
                    break;
                }
            }
        }

        if (newIndex == -1)
        {
            return EarthJson(null, null);
        }

        if (extendExisting)
        {
            var existingBoost = boosts.ActiveBoosts[newIndex];
            Debug.Assert(existingBoost is not null);

            boosts.ActiveBoosts[newIndex] = new BoostsEF.ActiveBoost(existingBoost.InstanceId, existingBoost.ItemId, existingBoost.StartTime, existingBoost.Duration + TimeSpan.FromMilliseconds(item.BoostInfo.Duration));
        }
        else
        {
            boosts.ActiveBoosts[newIndex] = new BoostsEF.ActiveBoost(Guid.NewGuid(), itemId, requestStartedOn, TimeSpan.FromMilliseconds(item.BoostInfo.Duration));
            if (item.BoostInfo.Effects.Any(effect => effect.Type is Catalog.ItemsCatalogR.Item.BoostEffectType.HEALTH))
            {
                // TODO: determine if we should add new player health straight away
                profileChanged = true;
            }
        }

        await ActivityLogUtils.AddEntryAsync(_earthDb, results, accountId, new BoostActivatedEntryEF(accountId, requestStartedOn, itemId), cancellationToken);

        await _earthDb.SaveChangesAsync(cancellationToken);

        results.Inventory();
        results.Boosts();

        results.Profile(profileChanged);

        return EarthJson(null, new EarthApiResponse.UpdatesResponse(await results.BuildAsync(_earthDb, accountId, cancellationToken)));
    }

    [HttpPost("boosts/minifigs/{productId}/{id}/activate")]
    public async Task<Results<ContentHttpResult, BadRequest>> ActivateMiniFig(string productId, string id, CancellationToken cancellationToken)
    {
        if (!TryGetProfileId(out var accountId))
        {
            return TypedResults.BadRequest();
        }

        if (string.IsNullOrWhiteSpace(productId) || string.IsNullOrWhiteSpace(id))
        {
            return TypedResults.BadRequest();
        }

        if (!_catalog.NfcBoostsCatalog.MiniFigs.TryGetValue(productId, out var miniFig) || miniFig is null || miniFig.BoostMetadata is null || miniFig.Deprecated)
        {
            return TypedResults.BadRequest();
        }

        var requestStartedOn = HttpContext.GetTimestamp();

        TimeSpan duration = TimeSpan.Zero;
        if (!string.IsNullOrEmpty(miniFig.BoostMetadata.ActiveDuration) && TimeSpan.TryParse(miniFig.BoostMetadata.ActiveDuration, out var parsedActiveDuration))
        {
            duration = parsedActiveDuration;
        }
        else if (miniFig.BoostMetadata.Effects.FirstOrDefault(e => !string.IsNullOrEmpty(e.Duration)) is { } effectWithDuration && TimeSpan.TryParse(effectWithDuration.Duration, out var parsedEffectDuration))
        {
            duration = parsedEffectDuration;
        }

        if (duration <= TimeSpan.Zero)
        {
            return TypedResults.BadRequest();
        }

        var itemId = IdTranslator.ToGuid(productId);

        var boosts = await _earthDb.Boosts
            .AsTracking()
            .FirstAsync(boosts => boosts.Id == accountId, cancellationToken: cancellationToken);

        var profile = await _earthDb.Profiles
            .AsTracking()
            .FirstAsync(profile => profile.Id == accountId, cancellationToken: cancellationToken);

        var profileChanged = false;

        if (PruneBoostsAndUpdateProfile(boosts, profile, requestStartedOn, _catalog))
        {
            profileChanged = true;
        }

        var newIndex = -1;
        var extendExisting = false;
        for (var index = 0; index < boosts.ActiveBoosts.Length; index++)
        {
            var boost = boosts.ActiveBoosts[index];

            if (boost is not null && boost.ItemId == itemId)
            {
                newIndex = index;
                extendExisting = true;
                break;
            }
        }

        if (!extendExisting)
        {
            for (var index = 0; index < boosts.ActiveBoosts.Length; index++)
            {
                if (boosts.ActiveBoosts[index] is null)
                {
                    newIndex = index;
                    break;
                }
            }
        }

        if (newIndex == -1)
        {
            return EarthJson(null, null);
        }

        if (extendExisting)
        {
            var existingBoost = boosts.ActiveBoosts[newIndex];
            Debug.Assert(existingBoost is not null);

            boosts.ActiveBoosts[newIndex] = new BoostsEF.ActiveBoost(existingBoost.InstanceId, existingBoost.ItemId, existingBoost.StartTime, existingBoost.Duration + duration);
        }
        else
        {
            boosts.ActiveBoosts[newIndex] = new BoostsEF.ActiveBoost(Guid.NewGuid(), itemId, requestStartedOn, duration);
            if (miniFig.BoostMetadata.Effects.Any(effect => string.Equals(effect.Type, "Health", StringComparison.OrdinalIgnoreCase)))
            {
                profileChanged = true;
            }
        }

        var results = new ResultsEF.Builder();

        await ActivityLogUtils.AddEntryAsync(_earthDb, results, accountId, new BoostActivatedEntryEF(accountId, requestStartedOn, itemId), cancellationToken);

        await _earthDb.SaveChangesAsync(cancellationToken);

        results.Boosts();
        results.Profile(profileChanged);

        return EarthJson(null, new EarthApiResponse.UpdatesResponse(await results.BuildAsync(_earthDb, accountId, cancellationToken)));
    }

    [HttpDelete("boosts/{instanceId}")]
    public async Task<Results<ContentHttpResult, BadRequest>> DeactivateBoost(Guid instanceId, CancellationToken cancellationToken)
    {
        if (!TryGetProfileId(out var accountId))
        {
            return TypedResults.BadRequest();
        }

        var requestStartedOn = HttpContext.GetTimestamp();

        var boosts = await _earthDb.Boosts
            .AsTracking()
            .FirstAsync(boosts => boosts.Id == accountId, cancellationToken: cancellationToken);

        var profile = await _earthDb.Profiles
            .AsTracking()
            .FirstAsync(profile => profile.Id == accountId, cancellationToken: cancellationToken);

        var profileChanged = false;

        if (PruneBoostsAndUpdateProfile(boosts, profile, requestStartedOn, _catalog))
        {
            profileChanged = true;
        }

        var activeBoost = boosts.Get(instanceId);
        if (activeBoost is null)
        {
            return EarthJson(null, null);
        }

        var item = _catalog.ItemsCatalog.GetItem(activeBoost.ItemId);
        if (item is null || item.BoostInfo is null || !item.BoostInfo.CanBeRemoved)
        {
            return EarthJson(null, null);
        }

        for (var index = 0; index < boosts.ActiveBoosts.Length; index++)
        {
            var boost = boosts.ActiveBoosts[index];

            if (boost is not null && boost.InstanceId == instanceId)
            {
                boosts.ActiveBoosts[index] = null;
            }
        }

        if (item.BoostInfo.Effects.Any(effect => effect.Type is Catalog.ItemsCatalogR.Item.BoostEffectType.HEALTH))
        {
            profileChanged = true;
            var maxPlayerHealth = Common.Utils.BoostUtils.GetMaxPlayerHealth(boosts, requestStartedOn, _catalog);
            if (profile.Health > maxPlayerHealth)
            {
                profile.Health = maxPlayerHealth;
            }
        }

        await _earthDb.SaveChangesAsync(cancellationToken);

        var results = new ResultsEF.Builder()
            .Boosts()
            .Profile(profileChanged);

        return EarthJson(null, new EarthApiResponse.UpdatesResponse(await results.BuildAsync(_earthDb, accountId, cancellationToken)));
    }

    private static bool PruneBoostsAndUpdateProfile(BoostsEF boosts, ProfileEF profile, DateTimeOffset currentTime, Catalog catalog)
    {
        var profileChanged = false;
        var prunedBoosts = boosts.Prune(currentTime);
        if (prunedBoosts.Any(activeBoost =>
        {
            var item = catalog.ItemsCatalog.GetItem(activeBoost.ItemId);
            if (item?.BoostInfo?.Effects.Any(effect => effect.Type is Catalog.ItemsCatalogR.Item.BoostEffectType.HEALTH) == true)
            {
                return true;
            }

            var nfcBoost = catalog.NfcBoostsCatalog.MiniFigs.Values.FirstOrDefault(m => IdTranslator.ToGuid(m.Id) == activeBoost.ItemId);
            if (nfcBoost?.BoostMetadata?.Effects.Any(effect => string.Equals(effect.Type, "Health", StringComparison.OrdinalIgnoreCase)) == true)
            {
                return true;
            }

            return false;
        }))
        {
            profileChanged = true;
        }

        var maxPlayerHealth = Common.Utils.BoostUtils.GetMaxPlayerHealth(boosts, currentTime, catalog);
        if (profile.Health > maxPlayerHealth)
        {
            profile.Health = maxPlayerHealth;
            profileChanged = true;
        }

        return profileChanged;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Active boost {ItemId} has effect with activation {ActivationType}")]
    private partial void LogUnexpectedBoostActivation(Guid ItemId, Catalog.ItemsCatalogR.Item.BoostEffectActivation ActivationType);
}
