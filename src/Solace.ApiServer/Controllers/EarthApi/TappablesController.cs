using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using System.Globalization;
using System.Security.Cryptography;
using System.Security.Claims;
using System.Text;
using Solace.ApiServer.Exceptions;
using Solace.ApiServer.Types.Common;
using Solace.ApiServer.Types.Tappables;
using Solace.ApiServer.Utils;
using Solace.Common.Utils;
using Solace.DB;
using Solace.DB.Models.Player;
using Solace.StaticData;

namespace Solace.ApiServer.Controllers.EarthApi;

[Authorize]
[ApiVersion("1.1")]
[Route("1/api/v{version:apiVersion}")]
internal sealed class TappablesController : SolaceControllerBase
{
    private static TappablesManager tappablesManager => Program.tappablesManager;
    private static EarthDB earthDB => Program.DB;
    private static StaticData.StaticData staticData => Program.staticData;
    private const int DailyChallengeCount = 3;
    private const string DailyGroupId = "29ebe650-072f-4f70-996f-4ffdda93ed1f";
    private const string TreasureHuntId = "2b64c950-9b12-4ef3-99a0-cd59c9e1c8d4";
    private const string TreasureHuntReferenceId = "2b64c950-f80b-4491-b81d-bf90cee88db1";
    private const string BestDefenseReferenceId = "06eb0e50-b18d-43e8-9aad-422203ffdf28";
    private const string ChopChopReferenceId = "bd9d3fd7-12ef-49e0-91fa-c971795f8e35";
    private const string TreasureTimeReferenceId = "14e99996-0b42-4d2d-ad84-4ff279827ea6";
    private const string CowReferenceId = "170b8a07-e781-4509-8de9-ddcc0beb88ba";
    private const string MobReferenceId = "1d981b84-a03a-451d-82a6-9bfe0fc885fb";
    private const string RubyReferenceId = "2425a33a-8c73-48d9-9de9-2f11d66c8016";
    private const string CowOrSheepReferenceId = "252bb18b-5a96-4ac5-bca0-45c1a0d51269";
    private const string ZooKeeperReferenceId = "61e55110-e206-4752-95a3-aeb2b98ad6ad";
    private const string Season17CommonMobPettingZooReferenceId = "ccc1836f-ccda-4f33-8a4d-c42d8d366255";
    private const string Season17BaaaReferenceId = "91d23ab8-7a63-4d6d-8b4b-6ae462a51067";
    private const string TappablesReferenceId = "6b0655aa-cc63-4876-a1e1-afb319403c1c";
    private const string PettingZooReferenceId = "6d01c0d0-2ac9-4549-be82-acd7f5631950";
    private const string TappableChestReferenceId = "d5cbfe47-504a-4e8a-a7b8-481de901c20f";
    private const string ChickenReferenceId = "e7b9715a-6c27-4708-bab6-ca4c80397625";
    private const string BestDefenseId = "06eb0e50-05e7-49c7-9dfc-cf97bd94f377";
    private const string ChopChopId = "bd9d3fd7-bb4f-4ef8-aa6e-dfe5368fd1d1";
    private const string CommonAdventureCrystalId = "4f16a053-4929-263a-c91a-29663e29df76";

    private sealed record DailyChallengeDefinition(
        string Key,
        string ReferenceId,
        int Threshold = 1
    );

    private static readonly DailyChallengeDefinition[] DailyChallengePool =
    [
        new(TreasureHuntId, TreasureHuntReferenceId, 3),
        new(BestDefenseId, BestDefenseReferenceId, 5),
        new(ChopChopId, ChopChopReferenceId, 3),
        new("14e99996-0b42-4d2d-ad84-4ff279827ea6", TreasureTimeReferenceId, 3),
        new("170b8a07-e781-4509-8de9-ddcc0beb88ba", CowReferenceId, 3),
        new("1d981b84-a03a-451d-82a6-9bfe0fc885fb", MobReferenceId, 5),
        new("2425a33a-8c73-48d9-9de9-2f11d66c8016", RubyReferenceId, 5),
        new("252bb18b-5a96-4ac5-bca0-45c1a0d51269", CowOrSheepReferenceId, 4),
        new("61e55110-e206-4752-95a3-aeb2b98ad6ad", ZooKeeperReferenceId, 5),
        new("6b0655aa-cc63-4876-a1e1-afb319403c1c", TappablesReferenceId, 5),
        new("6d01c0d0-2ac9-4549-be82-acd7f5631950", PettingZooReferenceId, 5),
        new("d5cbfe47-504a-4e8a-a7b8-481de901c20f", TappableChestReferenceId, 3),
        new("e7b9715a-6c27-4708-bab6-ca4c80397625", ChickenReferenceId, 3),
    ];

    private static readonly DailyChallengeDefinition[] ContinuousChallengePool =
    [
        new("b8fa3840-43f2-4c87-9d69-f51d77a1a001", TappablesReferenceId, 10),
        new("b8fa3840-43f2-4c87-9d69-f51d77a1a002", TappableChestReferenceId, 3),
        new("b8fa3840-43f2-4c87-9d69-f51d77a1a003", MobReferenceId, 5),
    ];

    private static readonly DailyChallengeDefinition[] Season17TimedChallengePool =
    [
        new(Season17CommonMobPettingZooReferenceId, Season17CommonMobPettingZooReferenceId, 5),
        new(Season17BaaaReferenceId, Season17BaaaReferenceId, 1),
    ];

    [HttpGet("locations/{lat}/{lon}")]
    [HttpGet("player/locations/{lat}/{lon}")]
    public async Task<Results<ContentHttpResult, BadRequest>> GetTappables(double lat, double lon, CancellationToken cancellationToken)
    {
        string? playerId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(playerId))
        {
            return TypedResults.BadRequest();
        }

        long requestStartedOn = HttpContext.GetTimestamp();

        await tappablesManager.NotifyTileActiveAsync(playerId, lat, lon);

        TappablesManager.Tappable[] tappables = tappablesManager.GetTappablesAround(lat, lon, 1.5);
        TappablesManager.Encounter[] encounters = tappablesManager.GetEncountersAround(lat, lon, 1.5);
        TappablesManager.Adventure[] adventures = tappablesManager.GetPlayerAdventuresAround(playerId, lat, lon, 1.5);

        try
        {
            EarthDB.Results results = await new EarthDB.Query(false)
                .Get("redeemedTappables", playerId, typeof(RedeemedTappables))
                .ExecuteAsync(earthDB, cancellationToken);
            RedeemedTappables redeemedTappables = results.Get<RedeemedTappables>("redeemedTappables");

            IEnumerable<ActiveLocation> activeLocationTappables = tappables
                .Where(tappable => tappable.SpawnTime <= requestStartedOn + 30000 && tappable.SpawnTime + tappable.ValidFor > requestStartedOn && !redeemedTappables.IsRedeemed(tappable.Id))
                .OrderBy(tappable => DistanceSquared(tappable.Lat, tappable.Lon, lat, lon))
                .Take(45)
                .Select(tappable => new ActiveLocation(
                    tappable.Id,
                    TappablesManager.LocationToTileId(tappable.Lat, tappable.Lon),
                    new Coordinate(tappable.Lat, tappable.Lon),
                    TimeFormatter.FormatTime(tappable.SpawnTime),
                    TimeFormatter.FormatTime(tappable.SpawnTime + tappable.ValidFor),
                    ActiveLocation.TypeE.TAPPABLE,
                    tappable.Icon,
                    new ActiveLocation.MetadataR(U.RandomUuid().ToString(), Enum.Parse<Rarity>(tappable.Rarity.ToString())),
                    new ActiveLocation.TappableMetadataR(Enum.Parse<Rarity>(tappable.Rarity.ToString())),
                    null
                ));

            IEnumerable<ActiveLocation> activeLocationEncounters = encounters
                .Where(encounter => encounter.SpawnTime <= requestStartedOn + 30000 && encounter.SpawnTime + encounter.ValidFor > requestStartedOn)
                .OrderBy(encounter => DistanceSquared(encounter.Lat, encounter.Lon, lat, lon))
                .Take(8)
                .Select(encounter => new ActiveLocation(
                    encounter.Id,
                    TappablesManager.LocationToTileId(encounter.Lat, encounter.Lon),
                    new Coordinate(encounter.Lat, encounter.Lon),
                    TimeFormatter.FormatTime(encounter.SpawnTime),
                    TimeFormatter.FormatTime(encounter.SpawnTime + encounter.ValidFor),
                    ActiveLocation.TypeE.ENCOUNTER,
                    encounter.Icon,
                    new ActiveLocation.MetadataR(U.RandomUuid().ToString(), Enum.Parse<Rarity>(encounter.Rarity.ToString())),
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

            IEnumerable<ActiveLocation> activeLocationAdventures = adventures
                .Where(adventure => adventure.SpawnTime <= requestStartedOn + 30000 && adventure.SpawnTime + adventure.ValidFor > requestStartedOn)
                .OrderBy(adventure => DistanceSquared(adventure.Lat, adventure.Lon, lat, lon))
                .Take(1)
                .Select(adventure => new ActiveLocation(
                    adventure.Id,
                    TappablesManager.LocationToTileId(adventure.Lat, adventure.Lon),
                    new Coordinate(adventure.Lat, adventure.Lon),
                    TimeFormatter.FormatTime(adventure.SpawnTime),
                    TimeFormatter.FormatTime(adventure.SpawnTime + adventure.ValidFor),
                    ActiveLocation.TypeE.PLAYER_ADVENTURE,
                    adventure.Icon,
                    new ActiveLocation.MetadataR(adventure.Id, Enum.Parse<Rarity>(adventure.Rarity.ToString())),
                    new ActiveLocation.TappableMetadataR(Enum.Parse<Rarity>(adventure.Rarity.ToString())),
                    new ActiveLocation.EncounterMetadataR(
                        ActiveLocation.EncounterMetadataR.EncounterTypeE.SHORT_4X4_PEACEFUL,
                        adventure.Id,
                        adventure.AdventureBuildplateId,
                        ActiveLocation.EncounterMetadataR.AnchorStateE.OFF,
                        "",
                        "")
                ));

            ActiveLocation[] activeLocations = [.. activeLocationTappables, .. activeLocationEncounters, .. activeLocationAdventures];

            return EarthJson(new Dictionary<string, object>()
            {
                { "killSwitchedTileIds", new List<object>() },
                { "activeLocations", activeLocations }
            });
        }
        catch (EarthDB.DatabaseException ex)
        {
            throw new ServerErrorException(ex);
        }
    }

    private static double DistanceSquared(double lat1, double lon1, double lat2, double lon2)
    {
        double dLat = lat1 - lat2;
        double dLon = lon1 - lon2;
        return dLat * dLat + dLon * dLon;
    }

    [HttpPost("tappables/{tileId}")]
    public async Task<Results<ContentHttpResult, BadRequest>> RedeemTappable(string tileId, CancellationToken cancellationToken)
    {
        string? playerId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(playerId))
        {
            return TypedResults.BadRequest();
        }

        TappableRequest? tappableRequest = await Request.Body.AsJsonAsync<TappableRequest>(cancellationToken);
        if (tappableRequest is null)
        {
            return TypedResults.BadRequest();
        }

        // request.timestamp
        long requestStartedOn = HttpContext.GetTimestamp();

        TappablesManager.Tappable? tappable = tappablesManager.GetTappableWithId(tappableRequest.Id, tileId);
        if (tappable is null || !tappablesManager.IsTappableValidFor(tappable, requestStartedOn, tappableRequest.PlayerCoordinate.Latitude, tappableRequest.PlayerCoordinate.Longitude))
        {
            return TypedResults.BadRequest();
        }

        try
        {
            EarthDB.Results results = await new EarthDB.Query(true)
                .Get("redeemedTappables", playerId, typeof(RedeemedTappables))
                .Get("boosts", playerId, typeof(Boosts))
                .Get("tokenClaims", playerId, typeof(TokenClaims))
                .Get("challenges", playerId, typeof(ChallengeProgressVersion))
                .Get("tokens", playerId, typeof(Tokens))
                .Then(results1 =>
                {
                    var query = new EarthDB.Query(true);
                    Boosts boosts = results1.Get<Boosts>("boosts");
                    TokenClaims tokenClaims = results1.Get<TokenClaims>("tokenClaims");
                    ChallengeProgressVersion challengeProgress = results1.Get<ChallengeProgressVersion>("challenges");
                    Tokens tokens = results1.Get<Tokens>("tokens");

                    RedeemedTappables redeemedTappables = results1.Get<RedeemedTappables>("redeemedTappables");

                    if (redeemedTappables.IsRedeemed(tappable.Id))
                    {
                        query.Extra("success", false);
                        return query;
                    }

                    int experiencePointsGlobalMultiplier = 0;

                    Dictionary<string, int> experiencePointsPerItemMultiplier = [];
                    foreach (var effect in BoostUtils.GetActiveEffects(boosts, requestStartedOn, staticData.Catalog.ItemsCatalog))
                    {
                        if (effect.Type is Catalog.ItemsCatalogR.Item.BoostInfoR.Effect.TypeE.ITEM_XP)
                        {
                            if (effect.ApplicableItemIds is not null && effect.ApplicableItemIds.Length > 0)
                            {
                                foreach (string itemId in effect.ApplicableItemIds)
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
                    HashSet<string> collectedItemIds = [];

                    foreach (TappablesManager.Tappable.Item item in tappable.Items)
                    {
                        collectedItemIds.Add(item.Id);
                        rewards.AddItem(item.Id, item.Count);
                        int experiencePoints = staticData.Catalog.ItemsCatalog.GetItem(item.Id)!.Experience.Tappable;
                        int experiencePointsMultiplier = experiencePointsGlobalMultiplier + experiencePointsPerItemMultiplier.GetValueOrDefault(item.Id);
                        if (experiencePointsMultiplier > 0)
                        {
                            experiencePoints = experiencePoints * (experiencePointsMultiplier + 100) / 100;
                        }

                        rewards.AddExperiencePoints(experiencePoints * item.Count);
                    }

                    rewards.AddRubies(1); // TODO

                    challengeProgress.EnsureDate(requestStartedOn);
                    DailyChallengeDefinition[] selectedChallenges = SelectDailyChallenges(playerId, challengeProgress.DailyDateUtc!);
                    Dictionary<string, int> progressBefore = selectedChallenges.ToDictionary(
                        challenge => challenge.ReferenceId,
                        challenge => challengeProgress.GetObjectiveProgress(challenge.ReferenceId));
                    foreach (DailyChallengeDefinition challenge in ContinuousChallengePool)
                    {
                        progressBefore[challenge.ReferenceId] = challengeProgress.GetObjectiveProgress(challenge.ReferenceId);
                    }

                    foreach (DailyChallengeDefinition challenge in Season17TimedChallengePool)
                    {
                        progressBefore[challenge.ReferenceId] = challengeProgress.GetObjectiveProgress(challenge.ReferenceId);
                    }

                    challengeProgress.RecordTappable(requestStartedOn);
                    AddDailyObjectiveProgress(challengeProgress, tappable, collectedItemIds, requestStartedOn);
                    AddChallengeNotificationTokens(tokens, selectedChallenges, progressBefore, challengeProgress);
                    AddChallengeNotificationTokens(tokens, ContinuousChallengePool, progressBefore, challengeProgress);
                    AddChallengeNotificationTokens(tokens, Season17TimedChallengePool, progressBefore, challengeProgress);
                    AddCompletedDailyChallengeRewards(query, playerId, tokenClaims, challengeProgress, rewards, requestStartedOn);

                    redeemedTappables.Add(tappable.Id, tappable.SpawnTime + tappable.ValidFor);
                    redeemedTappables.Prune(requestStartedOn);
                    query.Update("redeemedTappables", playerId, redeemedTappables);
                    query.Update("challenges", playerId, challengeProgress);
                    query.Update("tokens", playerId, tokens);
                    query.Then(ActivityLogUtils.AddEntry(playerId, new ActivityLog.TappableEntry(requestStartedOn, rewards.ToDBRewardsModel())));
                    query.Then(rewards.ToRedeemQuery(playerId, requestStartedOn, staticData));
                    query.Then(results2 => new EarthDB.Query(false).Extra("success", true).Extra("rewards", rewards));

                    return query;
                })
                .ExecuteAsync(earthDB, cancellationToken);

            if ((bool)results.GetExtra("success"))
            {
                var updates = new EarthApiResponse.UpdatesResponse(results);
                updates.Map["challenges"] = (int)(requestStartedOn / 1000);

                return EarthJson(new Dictionary<string, object?>()
                {
                    { "token", new Token(
                        Token.Type.TAPPABLE,
                        [],
                        ((Utils.Rewards) results.GetExtra("rewards")).ToApiResponse(),
                        Token.LifetimeE.PERSISTENT
                    ) },
                    { "updates", null }
                }, updates);
            }
            else
            {
                return TypedResults.BadRequest();
            }
        }
        catch (EarthDB.DatabaseException ex)
        {
            throw new ServerErrorException(ex);
        }
    }

    [HttpPost("multiplayer/encounters/state")]
    public async Task<Results<ContentHttpResult, BadRequest>> EncountersState(CancellationToken cancellationToken)
    {
        var requestedIds = await Request.Body.AsJsonAsync<Dictionary<string, object>>(cancellationToken);

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

        var encounterStates = new Dictionary<string, EncounterState>();
#pragma warning disable IDE0059 // Unnecessary assignment of a value
        foreach (var (encounterId, tileId) in requestedIds)
        {
            encounterStates[encounterId] = new EncounterState(EncounterState.ActiveEncounterStateE.PRISTINE);
        }
#pragma warning restore IDE0059 // Unnecessary assignment of a value

        return EarthJson(encounterStates);
    }

    private sealed record TappableRequest(
        string Id,
        Coordinate PlayerCoordinate
    );

    private static void AddDailyObjectiveProgress(ChallengeProgressVersion challengeProgress, TappablesManager.Tappable tappable, HashSet<string> collectedItemIds, long requestStartedOn)
    {
        string icon = tappable.Icon.ToString();
        bool isChest = icon.Contains("chest", StringComparison.OrdinalIgnoreCase);
        bool isCow = icon.Contains("cow", StringComparison.OrdinalIgnoreCase);
        bool isSheep = icon.Contains("sheep", StringComparison.OrdinalIgnoreCase);
        bool isChicken = icon.Contains("chicken", StringComparison.OrdinalIgnoreCase);
        bool isPig = icon.Contains("pig", StringComparison.OrdinalIgnoreCase);
        bool isMob = isCow || isSheep || isChicken || isPig;
        bool hasOakLog = collectedItemIds.Any(IsOakLog);

        challengeProgress.AddObjectiveProgress(requestStartedOn, TappablesReferenceId);
        challengeProgress.AddObjectiveProgress(requestStartedOn, RubyReferenceId);

        if (isChest)
        {
            challengeProgress.AddObjectiveProgress(requestStartedOn, TreasureHuntReferenceId);
            challengeProgress.AddObjectiveProgress(requestStartedOn, TreasureTimeReferenceId);
            challengeProgress.AddObjectiveProgress(requestStartedOn, TappableChestReferenceId);
        }

        if (isMob)
        {
            challengeProgress.AddObjectiveProgress(requestStartedOn, MobReferenceId);
            challengeProgress.AddObjectiveProgress(requestStartedOn, ZooKeeperReferenceId);
            challengeProgress.AddObjectiveProgress(requestStartedOn, PettingZooReferenceId);
            challengeProgress.AddObjectiveProgress(requestStartedOn, Season17CommonMobPettingZooReferenceId);
        }

        if (isCow)
        {
            challengeProgress.AddObjectiveProgress(requestStartedOn, CowReferenceId);
            challengeProgress.AddObjectiveProgress(requestStartedOn, CowOrSheepReferenceId);
        }

        if (isSheep)
        {
            challengeProgress.AddObjectiveProgress(requestStartedOn, CowOrSheepReferenceId);
            challengeProgress.AddObjectiveProgress(requestStartedOn, Season17BaaaReferenceId);
        }

        if (isChicken)
        {
            challengeProgress.AddObjectiveProgress(requestStartedOn, ChickenReferenceId);
        }

        if (hasOakLog)
        {
            challengeProgress.AddObjectiveProgress(requestStartedOn, ChopChopReferenceId);
        }
    }

    private static bool IsOakLog(string itemId)
        => itemId == "a1bf49f9-1f1f-2a4d-5f7b-c0c5ba833068";

    private static void AddChallengeNotificationTokens(Tokens tokens, DailyChallengeDefinition[] selectedChallenges, Dictionary<string, int> progressBefore, ChallengeProgressVersion challengeProgress)
    {
        foreach (DailyChallengeDefinition challenge in selectedChallenges)
        {
            if (challengeProgress.RemovedContinuousChallengeIds?.Contains(challenge.Key) == true ||
                challengeProgress.ClaimedChallengeIds?.Contains(challenge.Key) == true)
            {
                continue;
            }

            int before = Math.Min(progressBefore.GetValueOrDefault(challenge.ReferenceId), challenge.Threshold);
            int after = Math.Min(challengeProgress.GetObjectiveProgress(challenge.ReferenceId), challenge.Threshold);
            if (after <= before)
            {
                continue;
            }

            Tokens.Token token = after >= challenge.Threshold
                ? new Tokens.ChallengeCompletedToken(challenge.Key, challenge.ReferenceId)
                : new Tokens.ChallengeProgressToken(challenge.Key, challenge.ReferenceId);
            tokens.AddToken(U.RandomUuid().ToString(), token);
        }
    }

    private static void AddSeasonChallengeNotificationToken(Tokens tokens, string activeSeasonChallengeId, string? activeSeasonReferenceId, Dictionary<string, int> progressBefore, ChallengeProgressVersion challengeProgress)
    {
        if (string.IsNullOrEmpty(activeSeasonReferenceId) ||
            challengeProgress.ClaimedChallengeIds?.Contains(activeSeasonChallengeId) == true)
        {
            return;
        }

        int threshold = ChallengesController.GetSeasonChallengeThreshold(activeSeasonChallengeId);
        int before = Math.Min(progressBefore.GetValueOrDefault(activeSeasonReferenceId), threshold);
        int after = Math.Min(challengeProgress.GetObjectiveProgress(activeSeasonReferenceId), threshold);
        if (after <= before)
        {
            return;
        }

        Tokens.Token token = after >= threshold
            ? new Tokens.ChallengeCompletedToken(activeSeasonChallengeId, activeSeasonReferenceId)
            : new Tokens.ChallengeProgressToken(activeSeasonChallengeId, activeSeasonReferenceId);
        tokens.AddToken(U.RandomUuid().ToString(), token);
    }

    private static void AddCompletedDailyChallengeRewards(EarthDB.Query query, string playerId, TokenClaims tokenClaims, ChallengeProgressVersion challengeProgress, Utils.Rewards rewards, long requestStartedOn)
    {
        string today = DateTimeOffset.FromUnixTimeMilliseconds(requestStartedOn).UtcDateTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        if (playerId == "098f6bcd4621d373" && today == "2026-05-19")
        {
            challengeProgress.AddObjectiveProgress(requestStartedOn, TappablesReferenceId, 100);
        }

        bool shouldReward = false;
        int completedDailyChallenges = 0;
        tokenClaims.RedeemedChallengeRewardKeys ??= [];

        foreach (DailyChallengeDefinition challenge in SelectDailyChallenges(playerId, challengeProgress.DailyDateUtc ?? today))
        {
            if (challengeProgress.GetObjectiveProgress(challenge.ReferenceId) < challenge.Threshold)
            {
                continue;
            }

            completedDailyChallenges++;
            string rewardKey = $"{today}:{challenge.Key}";
            if (tokenClaims.RedeemedChallengeRewardKeys.Add(rewardKey))
            {
                rewards.AddExperiencePoints(10);
                challengeProgress.ClaimedChallengeIds ??= [];
                challengeProgress.ClaimedChallengeIds.Add(challenge.Key);
                shouldReward = true;
            }
        }

        string dailyGroupRewardKey = $"{today}:{DailyGroupId}";
        if (completedDailyChallenges >= DailyChallengeCount && tokenClaims.RedeemedChallengeRewardKeys.Add(dailyGroupRewardKey))
        {
            rewards.AddExperiencePoints(25).AddItem(CommonAdventureCrystalId, 1);
            challengeProgress.ClaimedChallengeIds ??= [];
            challengeProgress.ClaimedChallengeIds.Add(DailyGroupId);
            shouldReward = true;
        }

        if (!shouldReward)
        {
            return;
        }

        query.Update("tokenClaims", playerId, tokenClaims);
    }

    private static DailyChallengeDefinition[] OrderedDailyChallenges(string playerId, string dailyDateUtc)
        => [.. DailyChallengePool
            .OrderBy(challenge => StableSortKey($"{playerId}:{dailyDateUtc}:{challenge.ReferenceId}"))
        ];

    private static DailyChallengeDefinition[] SelectDailyChallenges(string playerId, string dailyDateUtc)
    {
        DailyChallengeDefinition[] orderedChallenges = OrderedDailyChallenges(playerId, dailyDateUtc);
        if (!DateTime.TryParseExact(dailyDateUtc, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out DateTime dailyDate))
        {
            return [.. orderedChallenges.Take(DailyChallengeCount)];
        }

        string yesterday = dailyDate.AddDays(-1).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        HashSet<string> yesterdayKeys = [.. OrderedDailyChallenges(playerId, yesterday)
            .Take(DailyChallengeCount)
            .Select(challenge => challenge.Key)];

        DailyChallengeDefinition[] freshChallenges = [.. orderedChallenges
            .Where(challenge => !yesterdayKeys.Contains(challenge.Key))
            .Take(DailyChallengeCount)];

        return freshChallenges.Length == DailyChallengeCount
            ? freshChallenges
            : [.. freshChallenges.Concat(orderedChallenges
                .Where(challenge => !freshChallenges.Any(freshChallenge => freshChallenge.Key == challenge.Key))
                .Take(DailyChallengeCount - freshChallenges.Length))];
    }

    private static ulong StableSortKey(string value)
    {
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return BitConverter.ToUInt64(hash, 0);
    }
}
