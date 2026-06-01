using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Serilog;
using System.Diagnostics;
using System.Security.Claims;
using Solace.ApiServer.Exceptions;
using Solace.ApiServer.Utils;
using Solace.Common.Utils;
using Solace.DB;
using Solace.DB.Models.Player;
using Solace.StaticData;
using Effect = Solace.ApiServer.Types.Common.Effect;

namespace Solace.ApiServer.Controllers.EarthApi;

[Authorize]
[ApiVersion("1.1")]
[Route("1/api/v{version:apiVersion}")]
internal sealed class BoostsController : SolaceControllerBase
{
    private static EarthDB earthDB => Program.DB;
    private static Catalog catalog => Program.staticData.Catalog;

    private sealed record ActiveBoostInfo(
        Boosts.ActiveBoost ActiveBoost,
        Catalog.ItemsCatalogR.Item.BoostInfoR BoostInfo
    );

    private sealed record ActiveMiniFigInfo(
        Boosts.ActiveMiniFig ActiveMiniFig,
        Catalog.NFCBoostsCatalogR.MiniFig MiniFig
    );

    [HttpGet("boosts")]
    public async Task<Results<ContentHttpResult, BadRequest>> GetBoosts(CancellationToken cancellation)
    {
        string? playerId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(playerId))
        {
            return TypedResults.BadRequest();
        }

        long requestStartedOn = HttpContext.GetTimestamp();

        EarthDB.Results results;
        try
        {
            results = await new EarthDB.Query(true)
                .Get("boosts", playerId, typeof(Boosts))
                .Get("profile", playerId, typeof(Profile))
                .Then(results1 =>
                {
                    // I know this is ugly, we're making changes to the database in response to a GET request, but if we don't then the client won't correctly update the player health bar in the UI

                    Boosts boosts = results1.Get<Boosts>("boosts");
                    Profile profile = results1.Get<Profile>("profile");

                    bool profileChanged = PruneBoostsAndUpdateProfile(boosts, profile, requestStartedOn, catalog.ItemsCatalog);
                    bool miniFigsChanged = boosts.PruneMiniFigs(requestStartedOn).Length > 0;
                    if (!profileChanged && !miniFigsChanged)
                    {
                        return new EarthDB.Query(false)
                            .Extra("boosts", boosts);
                    }

                    var updateQuery = new EarthDB.Query(true)
                        .Update("boosts", playerId, boosts)
                        .Extra("boosts", boosts);

                    if (profileChanged)
                    {
                        updateQuery.Update("profile", playerId, profile);
                    }

                    return updateQuery;
                })
                .ExecuteAsync(earthDB, cancellation);
        }
        catch (EarthDB.DatabaseException exception)
        {
            throw new ServerErrorException(exception);
        }

        var boosts = (Boosts)results.GetExtra("boosts");

        Types.Boost.Boosts.Potion?[] potions = [.. boosts.ActiveBoosts.Select(activeBoost =>
        {
            return activeBoost is null
                ? null
                : new Types.Boost.Boosts.Potion(true, activeBoost.ItemId, activeBoost.InstanceId, TimeFormatter.FormatTime(activeBoost.StartTime + activeBoost.Duration));
        })];

        Dictionary<string, ActiveBoostInfo> activeBoostsWithInfo = [];
        foreach (Boosts.ActiveBoost? activeBoost in boosts.ActiveBoosts)
        {
            if (activeBoost is null)
            {
                continue;
            }

            Catalog.ItemsCatalogR.Item? item = catalog.ItemsCatalog.GetItem(activeBoost.ItemId);
            if (item is null || item.BoostInfo is null)
            {
                continue;
            }

            ActiveBoostInfo? existingActiveBoostInfo = activeBoostsWithInfo.GetValueOrDefault(item.BoostInfo.Name);
            if (existingActiveBoostInfo is not null && existingActiveBoostInfo.BoostInfo.Level > item.BoostInfo.Level)
            {
                continue;
            }

            activeBoostsWithInfo[item.BoostInfo.Name] = new ActiveBoostInfo(activeBoost, item.BoostInfo);
        }

        LinkedList<Types.Boost.Boosts.ActiveEffect> activeEffects = [];
        LinkedList<Types.Boost.Boosts.ScenarioBoost> triggeredOnDeathBoosts = [];
        foreach (ActiveBoostInfo activeBoostInfo in activeBoostsWithInfo.Values)
        {
            if (!activeBoostInfo.BoostInfo.TriggeredOnDeath)
            {
                foreach (Catalog.ItemsCatalogR.Item.BoostInfoR.Effect effect in activeBoostInfo.BoostInfo.Effects)
                {
                    if (effect.Activation != Catalog.ItemsCatalogR.Item.BoostInfoR.Effect.ActivationE.TIMED)
                    {
                        Log.Warning($"Active boost {activeBoostInfo.ActiveBoost.ItemId} has effect with activation {effect.Activation}");
                        continue;
                    }

                    activeEffects.AddLast(new Types.Boost.Boosts.ActiveEffect(BoostUtils.BoostEffectToApiResponse(effect, activeBoostInfo.ActiveBoost.Duration), TimeFormatter.FormatTime(activeBoostInfo.ActiveBoost.StartTime + activeBoostInfo.ActiveBoost.Duration)));
                }
            }
            else
            {
                LinkedList<Effect> effects = [];
                foreach (Catalog.ItemsCatalogR.Item.BoostInfoR.Effect effect in activeBoostInfo.BoostInfo.Effects)
                {
                    if (effect.Activation != Catalog.ItemsCatalogR.Item.BoostInfoR.Effect.ActivationE.TRIGGERED)
                    {
                        Log.Warning($"Active boost {activeBoostInfo.ActiveBoost.ItemId} has effect with activation {effect.Activation}");
                        continue;
                    }

                    effects.AddLast(BoostUtils.BoostEffectToApiResponse(effect, activeBoostInfo.ActiveBoost.Duration));
                }

                triggeredOnDeathBoosts.AddLast(new Types.Boost.Boosts.ScenarioBoost(true, activeBoostInfo.ActiveBoost.InstanceId, [.. effects], TimeFormatter.FormatTime(activeBoostInfo.ActiveBoost.StartTime + activeBoostInfo.ActiveBoost.Duration)));
            }
        }

        Dictionary<string, Types.Boost.Boosts.ScenarioBoost[]> scenarioBoosts = [];
        if (triggeredOnDeathBoosts.Count > 0)
        {
            scenarioBoosts["death"] = [.. triggeredOnDeathBoosts];
        }

        // The 0.33 client is fragile around partially implemented Boost Mini state.
        // Keep NFC activation accepted, but do not surface minifig records/effects in the boosts menu yet.
        Types.Boost.Boosts.MiniFig?[] miniFigs = new Types.Boost.Boosts.MiniFig?[5];
        Dictionary<string, Types.Boost.Boosts.MiniFigRecord> miniFigRecords = [];

        BoostUtils.StatModiferValues statModiferValues = BoostUtils.GetActiveStatModifiers(boosts, requestStartedOn, catalog.ItemsCatalog);
        int tappableInteractionRadiusExtraMeters = statModiferValues.TappableInteractionRadiusExtraMeters;
        int experiencePointRate = 0;
        int itemExperiencePointRates = 0;
        int attackMultiplier = statModiferValues.AttackMultiplier;
        int defenseMultiplier = statModiferValues.DefenseMultiplier;
        int miningSpeedMultiplier = statModiferValues.MiningSpeedMultiplier;
        int maxPlayerHealthMultiplier = statModiferValues.MaxPlayerHealthMultiplier;
        int craftingSpeedMultiplier = statModiferValues.CraftingSpeedMultiplier;
        int smeltingSpeedMultiplier = statModiferValues.SmeltingSpeedMultiplier;
        int foodMultiplier = statModiferValues.FoodMultiplier;

        var boostsResponse = new Types.Boost.Boosts(
            potions,
            miniFigs,
            [.. activeEffects],
            scenarioBoosts,
            new Types.Boost.Boosts.StatusEffectsR(
                tappableInteractionRadiusExtraMeters > 0 ? tappableInteractionRadiusExtraMeters + 70 : null,
                experiencePointRate > 0 ? experiencePointRate + 100 : null,
                itemExperiencePointRates > 0 ? itemExperiencePointRates + 100 : null,
                attackMultiplier > 0 ? attackMultiplier + 100 : null,
                defenseMultiplier > 0 ? defenseMultiplier + 100 : null,
                miningSpeedMultiplier > 0 ? miningSpeedMultiplier + 100 : null,
                maxPlayerHealthMultiplier > 0 ? 20 * maxPlayerHealthMultiplier / 100 + 20 : 20,
                craftingSpeedMultiplier > 0 ? craftingSpeedMultiplier / 100 + 1 : null,
                smeltingSpeedMultiplier > 0 ? smeltingSpeedMultiplier / 100 + 1 : null,
                foodMultiplier > 0 ? (foodMultiplier + 100) / 100f : null
            ),
            miniFigRecords,
            activeBoostsWithInfo.Count != 0
                ? TimeFormatter.FormatTime(
                    activeBoostsWithInfo.Values
                        .Select(activeBoostInfo => activeBoostInfo.ActiveBoost.StartTime + activeBoostInfo.ActiveBoost.Duration)
                        .Min()
                )
                : null
        );

        return EarthJson(boostsResponse, new EarthApiResponse.UpdatesResponse(results));
    }

    [HttpGet("boosts/players/{playerId}/latest")]
    public Task<Results<ContentHttpResult, BadRequest>> GetLatestBoosts(string playerId, CancellationToken cancellation)
        => GetBoosts(cancellation);

    [HttpPost("boosts/minifigs/{productId}/{id}/activate")]
    public async Task<Results<ContentHttpResult, BadRequest>> ActivateMiniFig(string productId, string id, CancellationToken cancellationToken)
    {
        string? playerId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(playerId))
        {
            return TypedResults.BadRequest();
        }

        Catalog.NFCBoostsCatalogR.MiniFig? miniFig = catalog.NfcBoostsCatalog.GetMiniFig(productId);
        string resolvedProductId = productId;
        string tagId = id;
        if (miniFig is null)
        {
            Catalog.NFCBoostsCatalogR.MiniFig? swappedMiniFig = catalog.NfcBoostsCatalog.GetMiniFig(id);
            if (swappedMiniFig is not null)
            {
                miniFig = swappedMiniFig;
                resolvedProductId = id;
                tagId = productId;
            }
        }

        if (miniFig is null)
        {
            miniFig = catalog.NfcBoostsCatalog.MiniFigs.FirstOrDefault(static candidate => !candidate.Deprecated);
            if (miniFig is not null)
            {
                resolvedProductId = miniFig.Id;
                tagId = $"{productId}:{id}";
                Log.Information("Unknown NFC minifig product {ProductId} tag {TagId}; using fallback product {FallbackProductId}", productId, id, resolvedProductId);
            }
        }

        if (miniFig is null || miniFig.Deprecated)
        {
            return TypedResults.BadRequest();
        }

        long requestStartedOn = HttpContext.GetTimestamp();
        long duration = GetMiniFigDuration(miniFig);

        try
        {
            EarthDB.Results results = await new EarthDB.Query(true)
                .Get("boosts", playerId, typeof(Boosts))
                .Then(results1 =>
                {
                    Boosts boosts = results1.Get<Boosts>("boosts");
                    boosts.PruneMiniFigs(requestStartedOn);

                    int slot = -1;
                    for (int index = 0; index < boosts.ActiveMiniFigs.Length; index++)
                    {
                        Boosts.ActiveMiniFig? activeMiniFig = boosts.ActiveMiniFigs[index];
                        if (activeMiniFig is not null && (activeMiniFig.TagId == tagId || activeMiniFig.ProductId == resolvedProductId))
                        {
                            slot = index;
                            break;
                        }
                    }

                    if (slot == -1)
                    {
                        for (int index = 0; index < boosts.ActiveMiniFigs.Length; index++)
                        {
                            if (boosts.ActiveMiniFigs[index] is null)
                            {
                                slot = index;
                                break;
                            }
                        }
                    }

                    if (slot == -1)
                    {
                        return new EarthDB.Query(false);
                    }

                    boosts.ActiveMiniFigs[slot] = new Boosts.ActiveMiniFig(U.RandomUuid().ToString(), resolvedProductId, tagId, requestStartedOn, duration);
                    boosts.MiniFigRecords[tagId] = boosts.MiniFigRecords.TryGetValue(tagId, out Boosts.MiniFigRecord? existingRecord)
                        ? existingRecord with { LastSeen = requestStartedOn, Activations = existingRecord.Activations + 1 }
                        : new Boosts.MiniFigRecord(resolvedProductId, tagId, requestStartedOn, 1);

                    return new EarthDB.Query(true)
                        .Update("boosts", playerId, boosts)
                        .Then(ActivityLogUtils.AddEntry(playerId, new ActivityLog.BoostActivatedEntry(requestStartedOn, resolvedProductId)));
                })
                .ExecuteAsync(earthDB, cancellationToken);

            return EarthJson(null, new EarthApiResponse.UpdatesResponse(results));
        }
        catch (EarthDB.DatabaseException exception)
        {
            throw new ServerErrorException(exception);
        }
    }

    [HttpPost("boosts/potions/{itemId}/activate")]
    public async Task<Results<ContentHttpResult, BadRequest>> ActivateBoost(string itemId, CancellationToken cancellationToken)
    {
        string? playerId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(playerId))
        {
            return TypedResults.BadRequest();
        }

        long requestStartedOn = HttpContext.GetTimestamp();

        Catalog.ItemsCatalogR.Item? item = catalog.ItemsCatalog.GetItem(itemId);

        if (item is null || item.BoostInfo is null || item.BoostInfo.Type is not Catalog.ItemsCatalogR.Item.BoostInfoR.TypeE.POTION)
        {
            return TypedResults.BadRequest();
        }

        try
        {
            EarthDB.Results results = await new EarthDB.Query(true)
                .Get("inventory", playerId, typeof(Inventory))
                .Get("boosts", playerId, typeof(Boosts))
                .Get("profile", playerId, typeof(Profile))
                .Then(results1 =>
                {
                    Inventory inventory = results1.Get<Inventory>("inventory");
                    Boosts boosts = results1.Get<Boosts>("boosts");
                    Profile profile = results1.Get<Profile>("profile");
                    bool profileChanged = false;

                    if (PruneBoostsAndUpdateProfile(boosts, profile, requestStartedOn, catalog.ItemsCatalog))
                    {
                        profileChanged = true;
                    }

                    if (!inventory.TakeItems(itemId, 1))
                    {
                        return new EarthDB.Query(false);
                    }

                    int newIndex = -1;
                    bool extendExisting = false;
                    for (int index = 0; index < boosts.ActiveBoosts.Length; index++)
                    {
                        var boost = boosts.ActiveBoosts[index];

                        if (boost is not null && boost.ItemId == itemId)
                        {
                            newIndex = index;
                            break;
                        }
                    }

                    if (!extendExisting)
                    {
                        for (int index = 0; index < boosts.ActiveBoosts.Length; index++)
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
                        return new EarthDB.Query(false);
                    }

                    if (extendExisting)
                    {
                        Boosts.ActiveBoost? existingBoost = boosts.ActiveBoosts[newIndex];
                        Debug.Assert(existingBoost is not null);

                        boosts.ActiveBoosts[newIndex] = new Boosts.ActiveBoost(existingBoost.InstanceId, existingBoost.ItemId, existingBoost.StartTime, existingBoost.Duration + item.BoostInfo.Duration);
                    }
                    else
                    {
                        boosts.ActiveBoosts[newIndex] = new Boosts.ActiveBoost(U.RandomUuid().ToString(), itemId, requestStartedOn, item.BoostInfo.Duration);
                        if (item.BoostInfo.Effects.Any(effect => effect.Type is Catalog.ItemsCatalogR.Item.BoostInfoR.Effect.TypeE.HEALTH))
                        {
                            // TODO: determine if we should add new player health straight away
                            profileChanged = true;
                        }
                    }

                    var updateQuery = new EarthDB.Query(true);
                    updateQuery.Update("inventory", playerId, inventory);
                    updateQuery.Update("boosts", playerId, boosts);

                    if (profileChanged)
                    {
                        updateQuery.Update("profile", playerId, profile);
                    }

                    updateQuery.Then(ActivityLogUtils.AddEntry(playerId, new ActivityLog.BoostActivatedEntry(requestStartedOn, itemId)));
                    return updateQuery;
                })
                .ExecuteAsync(earthDB, cancellationToken);

            return EarthJson(null, new EarthApiResponse.UpdatesResponse(results));
        }
        catch (EarthDB.DatabaseException exception)
        {
            throw new ServerErrorException(exception);
        }
    }

    [HttpDelete("boosts/{instanceId}")]
    [HttpDelete("boosts/{instanceId}/deactivate")]
    public async Task<Results<ContentHttpResult, BadRequest>> DeactivateBoost(string instanceId, CancellationToken cancellationToken)
    {
        string? playerId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(playerId))
        {
            return TypedResults.BadRequest();
        }

        long requestStartedOn = HttpContext.GetTimestamp();

        try
        {
            EarthDB.Results results = await new EarthDB.Query(true)
                .Get("boosts", playerId, typeof(Boosts))
                .Get("profile", playerId, typeof(Profile))
                .Then(results1 =>
                {
                    Boosts boosts = results1.Get<Boosts>("boosts");
                    Profile profile = results1.Get<Profile>("profile");
                    bool profileChanged = false;

                    if (PruneBoostsAndUpdateProfile(boosts, profile, requestStartedOn, catalog.ItemsCatalog))
                    {
                        profileChanged = true;
                    }

                    Boosts.ActiveBoost? activeBoost = boosts.Get(instanceId);
                    Boosts.ActiveMiniFig? activeMiniFig = boosts.GetMiniFig(instanceId);
                    if (activeBoost is null && activeMiniFig is null)
                    {
                        return new EarthDB.Query(false);
                    }

                    Catalog.ItemsCatalogR.Item? item = activeBoost is null ? null : catalog.ItemsCatalog.GetItem(activeBoost.ItemId);
                    Catalog.NFCBoostsCatalogR.MiniFig? miniFig = activeMiniFig is null ? null : catalog.NfcBoostsCatalog.GetMiniFig(activeMiniFig.ProductId);
                    if (activeBoost is not null && (item is null || item.BoostInfo is null || !item.BoostInfo.CanBeRemoved))
                    {
                        return new EarthDB.Query(false);
                    }

                    if (activeMiniFig is not null && (miniFig is null || !miniFig.BoostMetadata.CanBeRemoved))
                    {
                        return new EarthDB.Query(false);
                    }

                    for (int index = 0; index < boosts.ActiveBoosts.Length; index++)
                    {
                        var boost = boosts.ActiveBoosts[index];

                        if (boost is not null && boost.InstanceId == instanceId)
                        {
                            boosts.ActiveBoosts[index] = null;
                        }
                    }

                    for (int index = 0; index < boosts.ActiveMiniFigs.Length; index++)
                    {
                        var boost = boosts.ActiveMiniFigs[index];

                        if (boost is not null && boost.InstanceId == instanceId)
                        {
                            boosts.ActiveMiniFigs[index] = null;
                        }
                    }

                    if (item?.BoostInfo?.Effects.Any(effect => effect.Type is Catalog.ItemsCatalogR.Item.BoostInfoR.Effect.TypeE.HEALTH) == true
                        || miniFig?.BoostMetadata.Effects.Any(effect => effect.Type == "MaximumPlayerHealth") == true)
                    {
                        profileChanged = true;
                        int maxPlayerHealth = BoostUtils.GetMaxPlayerHealth(boosts, requestStartedOn, catalog.ItemsCatalog);
                        if (profile.Health > maxPlayerHealth)
                        {
                            profile.Health = maxPlayerHealth;
                        }
                    }

                    var updateQuery = new EarthDB.Query(true);
                    updateQuery.Update("boosts", playerId, boosts);
                    if (profileChanged)
                    {
                        updateQuery.Update("profile", playerId, profile);
                    }

                    return updateQuery;
                })
                .ExecuteAsync(earthDB, cancellationToken);

            return EarthJson(null, new EarthApiResponse.UpdatesResponse(results));
        }
        catch (EarthDB.DatabaseException exception)
        {
            throw new ServerErrorException(exception);
        }
    }

    private static bool PruneBoostsAndUpdateProfile(Boosts boosts, Profile profile, long currentTime, Catalog.ItemsCatalogR itemsCatalog)
    {
        bool profileChanged = false;
        Boosts.ActiveBoost[] prunedBoosts = boosts.Prune(currentTime);
        if (prunedBoosts.SelectMany(activeBoost => itemsCatalog.GetItem(activeBoost.ItemId)!.BoostInfo!.Effects).Any(effect => effect.Type is Catalog.ItemsCatalogR.Item.BoostInfoR.Effect.TypeE.HEALTH))
        {
            profileChanged = true;
        }

        int maxPlayerHealth = BoostUtils.GetMaxPlayerHealth(boosts, currentTime, itemsCatalog);
        if (profile.Health > maxPlayerHealth)
        {
            profile.Health = maxPlayerHealth;
            profileChanged = true;
        }

        return profileChanged;
    }

    private static long GetMiniFigDuration(Catalog.NFCBoostsCatalogR.MiniFig miniFig)
    {
        string? duration = miniFig.BoostMetadata.ActiveDuration
            ?? miniFig.BoostMetadata.Effects.FirstOrDefault(effect => !string.IsNullOrEmpty(effect.Duration))?.Duration;

        return duration is null
            ? 10 * 60 * 1000
            : TimeFormatter.ParseDuration(duration);
    }

    private static Effect NfcBoostEffectToApiResponse(Catalog.NFCBoostsCatalogR.EffectR effect)
        => new Effect(
            effect.Type,
            effect.Duration,
            effect.Value is null ? null : (int)Math.Round(effect.Value.Value),
            effect.Unit,
            effect.Targets,
            effect.Items,
            effect.ItemScenarios,
            effect.Activation,
            effect.ModifiesType
        );
}
