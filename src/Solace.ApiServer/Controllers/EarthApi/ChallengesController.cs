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
using Solace.ApiServer.Utils;
using Solace.Common;
using Solace.Common.Utils;
using Solace.DB;
using Rewards = Solace.ApiServer.Types.Common.Rewards;

namespace Solace.ApiServer.Controllers.EarthApi;

[Authorize]
[ApiVersion("1.1")]
[Route("1/api/v{version:apiVersion}/player/challenges")]
internal sealed class ChallengesController : ControllerBase
{
    private const int DailyChallengeCount = 3;
    private const string DailyGroupId = "c1a1beef-0d01-4b1a-8d1e-000000000001";
    private const string DailyReferenceId = "2619913d-6504-4c74-9fc9-e03649a70efc";
    private const string TreasureHuntReferenceId = "2b64c950-f80b-4491-b81d-bf90cee88db1";
    private const string BestDefenseReferenceId = "06eb0e50-b18d-43e8-9aad-422203ffdf28";
    private const string ChopChopReferenceId = "bd9d3fd7-12ef-49e0-91fa-c971795f8e35";
    private const string MobReferenceId = "1d981b84-a03a-451d-82a6-9bfe0fc885fb";
    private const string TappablesReferenceId = "6b0655aa-cc63-4876-a1e1-afb319403c1c";
    private const string TappableChestReferenceId = "d5cbfe47-504a-4e8a-a7b8-481de901c20f";
    private const string LegacySeasonPersonaReward1Id = "00000000-0000-0000-0000-000000000001";
    private const string LegacySeasonPersonaReward2Id = "00000000-0000-0000-0000-000000000002";
    private const string CommonAdventureCrystalId = "4f16a053-4929-263a-c91a-29663e29df76";
    internal const string ActiveSeasonId = "season_17";
    internal const string DefaultActiveSeasonChallengeId = "f0532069-a70a-4a01-8611-f770bb46d9cd";

    private sealed record ChallengeRecord(
        string ReferenceId,
        string? ParentId,
        string GroupId,
        string Duration,
        string Type,
        string Category,
        Rarity? Rarity,
        int Order,
        string EndTimeUtc,
        string State,
        bool IsComplete,
        int PercentComplete,
        int CurrentCount,
        int TotalThreshold,
        string[] PrerequisiteIds,
        string PrerequisiteLogicalCondition,
        Rewards Rewards,
        object ClientProperties
    );

    private sealed record DailyChallengeDefinition(
        string Key,
        string ReferenceId,
        int Threshold = 1
    );

    private static readonly DailyChallengeDefinition[] DailyChallengePool =
    [
        new("2b64c950-9b12-4ef3-99a0-cd59c9e1c8d4", TreasureHuntReferenceId, 3),
        new("06eb0e50-05e7-49c7-9dfc-cf97bd94f377", BestDefenseReferenceId, 5),
        new("bd9d3fd7-bb4f-4ef8-aa6e-dfe5368fd1d1", ChopChopReferenceId, 3),
        new("14e99996-0b42-4d2d-ad84-4ff279827ea6", "14e99996-0b42-4d2d-ad84-4ff279827ea6", 3),
        new("170b8a07-e781-4509-8de9-ddcc0beb88ba", "170b8a07-e781-4509-8de9-ddcc0beb88ba", 3),
        new("1d981b84-a03a-451d-82a6-9bfe0fc885fb", "1d981b84-a03a-451d-82a6-9bfe0fc885fb", 5),
        new("2425a33a-8c73-48d9-9de9-2f11d66c8016", "2425a33a-8c73-48d9-9de9-2f11d66c8016", 5),
        new("252bb18b-5a96-4ac5-bca0-45c1a0d51269", "252bb18b-5a96-4ac5-bca0-45c1a0d51269", 4),
        new("61e55110-e206-4752-95a3-aeb2b98ad6ad", "61e55110-e206-4752-95a3-aeb2b98ad6ad", 5),
        new("6b0655aa-cc63-4876-a1e1-afb319403c1c", "6b0655aa-cc63-4876-a1e1-afb319403c1c", 5),
        new("6d01c0d0-2ac9-4549-be82-acd7f5631950", "6d01c0d0-2ac9-4549-be82-acd7f5631950", 5),
        new("d5cbfe47-504a-4e8a-a7b8-481de901c20f", "d5cbfe47-504a-4e8a-a7b8-481de901c20f", 3),
        new("e7b9715a-6c27-4708-bab6-ca4c80397625", "e7b9715a-6c27-4708-bab6-ca4c80397625", 3),
    ];

    private static readonly DailyChallengeDefinition[] ContinuousChallengePool =
    [
        new("b8fa3840-43f2-4c87-9d69-f51d77a1a001", TappablesReferenceId, 10),
        new("b8fa3840-43f2-4c87-9d69-f51d77a1a002", TappableChestReferenceId, 3),
        new("b8fa3840-43f2-4c87-9d69-f51d77a1a003", MobReferenceId, 5),
    ];

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

    [HttpGet]
    public async Task<Results<ContentHttpResult, BadRequest>> Get(CancellationToken cancellationToken)
    {
        string? playerId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(playerId))
        {
            return TypedResults.BadRequest();
        }

        long endOfToday = DateTimeOffset.UtcNow.Date.AddDays(1).ToUnixTimeMilliseconds();
        string dailyEndTime = TimeFormatter.FormatTime(endOfToday);
        string seasonEndTime = TimeFormatter.FormatTime(endOfToday + 14 * 24 * 60 * 60 * 1000);
        long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        ChallengeProgressVersion progress;
        try
        {
            EarthDB.Results results = await new EarthDB.Query(false)
                .Get("challenges", playerId, typeof(ChallengeProgressVersion))
                .ExecuteAsync(Program.DB, cancellationToken);

            progress = results.Get<ChallengeProgressVersion>("challenges");
            progress.EnsureDate(now);
        }
        catch (EarthDB.DatabaseException ex)
        {
            throw new ServerErrorException(ex);
        }

        DailyChallengeDefinition[] dailyChallenges = SelectDailyChallenges(playerId, progress.DailyDateUtc!);
        int dailyCount = dailyChallenges.Count(challenge => progress.GetObjectiveProgress(challenge.ReferenceId) >= challenge.Threshold);
        bool dailyComplete = dailyCount >= DailyChallengeCount;
        bool dailyClaimed = progress.ClaimedChallengeIds?.Contains(DailyGroupId) == true;
        string dailyState = dailyClaimed ? "Claimed" : dailyComplete ? "Completed" : "Active";
        int dailyPercent = dailyCount * 100 / DailyChallengeCount;

        Response.Headers.CacheControl = "no-store";
        Response.Headers.ETag = $"\"challenges-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}\"";

        string activeSeasonChallengeId = SelectActiveSeasonChallengeId(progress, progress.ActiveSeasonChallengeId);

        var challenges = BuildSeasonChallenges(seasonEndTime, progress, activeSeasonChallengeId);
        if (!dailyClaimed)
        {
            challenges[DailyGroupId] = new ChallengeRecord(
                DailyReferenceId,
                null,
                DailyGroupId,
                "PersonalTimed",
                "Regular",
                "retention",
                null,
                0,
                dailyEndTime,
                dailyState,
                dailyComplete,
                dailyPercent,
                dailyCount,
                DailyChallengeCount,
                [],
                "And",
                new Rewards(0, 25, null, [new Rewards.Item(CommonAdventureCrystalId, 1)], [], [], [], []),
                new object()
            );
        }

        for (int index = 0; index < dailyChallenges.Length; index++)
        {
            DailyChallengeDefinition challenge = dailyChallenges[index];
            int threshold = challenge.Threshold;
            int currentCount = Math.Min(progress.GetObjectiveProgress(challenge.ReferenceId), threshold);
            bool isComplete = currentCount >= threshold;
            bool isClaimed = progress.ClaimedChallengeIds?.Contains(challenge.Key) == true;
            if (isComplete || isClaimed)
            {
                continue;
            }

            challenges[challenge.Key] = new ChallengeRecord(
                challenge.ReferenceId,
                null,
                DailyGroupId,
                "PersonalTimed",
                "Regular",
                "retention",
                Rarity.COMMON,
                index + 1,
                dailyEndTime,
                "Active",
                false,
                currentCount * 100 / threshold,
                currentCount,
                threshold,
                [],
                "And",
                new Rewards(0, 10, null, [], [], [], [], []),
                new object()
            );
        }

        for (int index = 0; index < ContinuousChallengePool.Length; index++)
        {
            DailyChallengeDefinition challenge = ContinuousChallengePool[index];
            if (progress.RemovedContinuousChallengeIds?.Contains(challenge.Key) == true ||
                progress.ClaimedChallengeIds?.Contains(challenge.Key) == true)
            {
                continue;
            }

            int threshold = challenge.Threshold;
            int currentCount = Math.Min(progress.GetObjectiveProgress(challenge.ReferenceId), threshold);
            bool isComplete = currentCount >= threshold;
            if (isComplete)
            {
                continue;
            }

            challenges[challenge.Key] = new ChallengeRecord(
                challenge.ReferenceId,
                null,
                challenge.Key,
                "PersonalContinuous",
                "Regular",
                "retention",
                Rarity.COMMON,
                index + 1,
                dailyEndTime,
                "Active",
                false,
                currentCount * 100 / threshold,
                currentCount,
                threshold,
                [],
                "And",
                new Rewards(0, 10, null, [], [], [], [], []),
                new object()
            );
        }

        string activeChallengeId = ContinuousChallengePool
            .FirstOrDefault(challenge =>
                progress.GetObjectiveProgress(challenge.ReferenceId) < challenge.Threshold &&
                progress.RemovedContinuousChallengeIds?.Contains(challenge.Key) != true &&
                progress.ClaimedChallengeIds?.Contains(challenge.Key) != true)
            ?.Key ?? DailyGroupId;

        string resp = Json.Serialize(new EarthApiResponse(new Dictionary<string, object>()
        {
            { "challenges", challenges },
            { "activeChallengeId", activeChallengeId },
            { "activeSeasonChallenge", activeSeasonChallengeId },
            { "activeSeasonId", ActiveSeasonId },
        }));
        return TypedResults.Content(resp, "application/json");
    }

    private static Dictionary<string, object> BuildSeasonChallenges(string seasonEndTime, ChallengeProgressVersion progress, string activeSeasonChallengeId)
    {
        var templates = Json.Deserialize<Dictionary<string, ChallengeRecord>>(Season17ChallengesJson) ?? [];
        return templates.ToDictionary(
            entry => entry.Key,
            entry => (object)ApplyProgress(entry.Key, entry.Value with { EndTimeUtc = seasonEndTime }, progress, activeSeasonChallengeId));
    }

    private static ChallengeRecord ApplyProgress(string challengeId, ChallengeRecord challenge, ChallengeProgressVersion progress, string activeSeasonChallengeId)
    {
        int objectiveCount = progress.GetObjectiveProgress(challenge.ReferenceId);
        bool isClaimed = progress.ClaimedChallengeIds?.Contains(challengeId) == true;
        if (challenge.IsComplete)
        {
            return challenge with { State = isClaimed ? "Claimed" : challenge.State };
        }

        int threshold = Math.Max(1, challenge.TotalThreshold);
        int currentCount = Math.Min(objectiveCount, threshold);
        bool isComplete = currentCount >= threshold;
        string state = isClaimed ? "Claimed" : isComplete ? "Completed" : challengeId == activeSeasonChallengeId ? "Active" : challenge.State;

        return challenge with
        {
            State = state,
            IsComplete = isComplete,
            PercentComplete = currentCount * 100 / threshold,
            CurrentCount = currentCount,
        };
    }

    internal static string SelectActiveSeasonChallengeId(ChallengeProgressVersion progress, string? requestedChallengeId = null)
    {
        var templates = Json.Deserialize<Dictionary<string, ChallengeRecord>>(Season17ChallengesJson) ?? [];
        string preferredChallengeId = string.IsNullOrWhiteSpace(requestedChallengeId)
            ? DefaultActiveSeasonChallengeId
            : requestedChallengeId;

        if (templates.ContainsKey(preferredChallengeId) &&
            progress.ClaimedChallengeIds?.Contains(preferredChallengeId) != true)
        {
            return preferredChallengeId;
        }

        int preferredOrder = templates.TryGetValue(preferredChallengeId, out ChallengeRecord? preferred)
            ? preferred.Order
            : -1;

        string? nextChallengeId = templates
            .Where(entry => IsSelectableActiveSeasonChallenge(entry.Key, entry.Value, templates, progress))
            .OrderBy(entry => entry.Value.Order <= preferredOrder ? 1 : 0)
            .ThenBy(entry => entry.Value.Order)
            .Select(entry => entry.Key)
            .FirstOrDefault();

        return nextChallengeId ?? DefaultActiveSeasonChallengeId;
    }

    private static bool IsSelectableActiveSeasonChallenge(
        string challengeId,
        ChallengeRecord challenge,
        Dictionary<string, ChallengeRecord> templates,
        ChallengeProgressVersion progress)
    {
        if (challenge.Duration != "Season" ||
            challenge.Category != ActiveSeasonId ||
            challengeId == challenge.GroupId ||
            progress.ClaimedChallengeIds?.Contains(challengeId) == true)
        {
            return false;
        }

        if (challenge.State != "Locked")
        {
            return true;
        }

        if (challenge.PrerequisiteIds.Length == 0)
        {
            return true;
        }

        bool IsSatisfied(string prerequisiteId)
            => progress.ClaimedChallengeIds?.Contains(prerequisiteId) == true ||
               (templates.TryGetValue(prerequisiteId, out ChallengeRecord? prerequisite) &&
                IsComplete(prerequisite, progress));

        return challenge.PrerequisiteLogicalCondition == "Or"
            ? challenge.PrerequisiteIds.Any(IsSatisfied)
            : challenge.PrerequisiteIds.All(IsSatisfied);
    }

    private static bool IsComplete(ChallengeRecord challenge, ChallengeProgressVersion progress)
    {
        if (challenge.IsComplete)
        {
            return true;
        }

        int threshold = Math.Max(1, challenge.TotalThreshold);
        return progress.GetObjectiveProgress(challenge.ReferenceId) >= threshold;
    }

    internal static string? GetSeasonChallengeReferenceId(string? challengeId)
    {
        if (string.IsNullOrWhiteSpace(challengeId))
        {
            return null;
        }

        var templates = Json.Deserialize<Dictionary<string, ChallengeRecord>>(Season17ChallengesJson) ?? [];
        return templates.TryGetValue(challengeId, out ChallengeRecord? challenge)
            ? challenge.ReferenceId
            : null;
    }

    internal static Rewards? GetSeasonChallengeRewards(string? challengeId)
    {
        if (string.IsNullOrWhiteSpace(challengeId))
        {
            return null;
        }

        if (challengeId == DailyGroupId)
        {
            return new Rewards(0, 25, null, [new Rewards.Item(CommonAdventureCrystalId, 1)], [], [], [], []);
        }

        if (DailyChallengePool.Any(challenge => challenge.Key == challengeId))
        {
            return new Rewards(0, 10, null, [], [], [], [], []);
        }

        if (ContinuousChallengePool.Any(challenge => challenge.Key == challengeId))
        {
            return new Rewards(0, 10, null, [], [], [], [], []);
        }

        var templates = Json.Deserialize<Dictionary<string, ChallengeRecord>>(Season17ChallengesJson) ?? [];
        return templates.TryGetValue(challengeId, out ChallengeRecord? challenge)
            ? challenge.Rewards
            : null;
    }

    internal static int GetSeasonChallengeThreshold(string? challengeId)
    {
        if (string.IsNullOrWhiteSpace(challengeId))
        {
            return 1;
        }

        var templates = Json.Deserialize<Dictionary<string, ChallengeRecord>>(Season17ChallengesJson) ?? [];
        return templates.TryGetValue(challengeId, out ChallengeRecord? challenge)
            ? Math.Max(1, challenge.TotalThreshold)
            : 1;
    }

    private const string Season17ChallengesJson = """
{
    "0b346237-79a4-4f24-a45a-e2e6284e3e56": {
        "referenceId": "65ec2dbb-c665-4173-81b2-56816576262f",
        "duration": "Season",
        "type": "Regular",
        "endTimeUtc": "2023-09-24T01:00:00Z",
        "rewards": {
            "inventory": [
                {
                    "id": "1d549957-68d0-730a-56f3-d33996738d84",
                    "amount": 1
                }
            ],
            "buildplates": [],
            "challenges": [],
            "personaItems": [],
            "utilityBlocks": []
        },
        "percentComplete": 0,
        "isComplete": false,
        "state": "Locked",
        "category": "season_17",
        "currentCount": 0,
        "totalThreshold": 1,
        "parentId": "70961d0f-a762-4846-a79c-576d685263f4",
        "order": 32,
        "rarity": null,
        "prerequisiteLogicalCondition": "Or",
        "prerequisiteIds": [
            "ab2e0734-62b1-4383-a239-d1b4d4a93dc4",
            "152a875e-2389-4245-8e1f-08f1915d6c7a"
        ],
        "groupId": "cc456b52-1586-4e75-b7e9-aa811f609567",
        "clientProperties": {}
    },
    "0d4c99ce-b485-4890-be69-caab88400df3": {
        "referenceId": "606e21fb-6781-4773-87f6-158a20729f04",
        "duration": "Season",
        "type": "Regular",
        "endTimeUtc": "2023-09-24T01:00:00Z",
        "rewards": {
            "inventory": [
                {
                    "id": "c44c331a-962f-19df-16a5-ff4bcc03722d",
                    "amount": 1
                }
            ],
            "buildplates": [],
            "challenges": [],
            "personaItems": [],
            "utilityBlocks": []
        },
        "percentComplete": 0,
        "isComplete": false,
        "state": "Locked",
        "category": "season_17",
        "currentCount": 0,
        "totalThreshold": 1,
        "parentId": "3d82b9c1-f4e0-4a20-b87e-9a11734bcb6a",
        "order": 11,
        "rarity": null,
        "prerequisiteLogicalCondition": "Or",
        "prerequisiteIds": [
            "242b6706-8112-4302-819b-bde98fd574f6",
            "fb13127e-d19d-48bd-a977-f830eecff180"
        ],
        "groupId": "cc456b52-1586-4e75-b7e9-aa811f609567",
        "clientProperties": {}
    },
    "152a875e-2389-4245-8e1f-08f1915d6c7a": {
        "referenceId": "1e4899f5-039e-48f0-a1cc-bd1bda871e96",
        "duration": "Season",
        "type": "Regular",
        "endTimeUtc": "2023-09-24T01:00:00Z",
        "rewards": {
            "inventory": [],
            "buildplates": [],
            "challenges": [],
            "personaItems": [
                "230f5996-04b2-4f0e-83e5-4056c7f1d946"
            ],
            "utilityBlocks": []
        },
        "percentComplete": 0,
        "isComplete": false,
        "state": "Locked",
        "category": "season_17",
        "currentCount": 0,
        "totalThreshold": 1,
        "parentId": "70961d0f-a762-4846-a79c-576d685263f4",
        "order": 34,
        "rarity": null,
        "prerequisiteLogicalCondition": "Or",
        "prerequisiteIds": [
            "ab2e0734-62b1-4383-a239-d1b4d4a93dc4",
            "0b346237-79a4-4f24-a45a-e2e6284e3e56"
        ],
        "groupId": "cc456b52-1586-4e75-b7e9-aa811f609567",
        "clientProperties": {}
    },
    "1ccbe15e-3a72-4d8b-ad0b-00a23ac72eb1": {
        "referenceId": "57f50f14-1b46-460b-82dc-e1220d53a15b",
        "duration": "Season",
        "type": "Regular",
        "endTimeUtc": "2023-09-24T01:00:00Z",
        "rewards": {
            "inventory": [
                {
                    "id": "d08fb88b-e670-2a8d-3b83-8edb363e7ba4",
                    "amount": 1
                }
            ],
            "buildplates": [],
            "challenges": [],
            "personaItems": [],
            "utilityBlocks": []
        },
        "percentComplete": 0,
        "isComplete": false,
        "state": "Locked",
        "category": "season_17",
        "currentCount": 0,
        "totalThreshold": 2,
        "parentId": "3d82b9c1-f4e0-4a20-b87e-9a11734bcb6a",
        "order": 4,
        "rarity": null,
        "prerequisiteLogicalCondition": "Or",
        "prerequisiteIds": [
            "f0532069-a70a-4a01-8611-f770bb46d9cd",
            "d8d2abd5-f318-4c34-a257-57b9691a2774"
        ],
        "groupId": "cc456b52-1586-4e75-b7e9-aa811f609567",
        "clientProperties": {}
    },
    "242b6706-8112-4302-819b-bde98fd574f6": {
        "referenceId": "543a0397-25c5-4e2d-b39d-6d6287c7cbed",
        "duration": "Season",
        "type": "Regular",
        "endTimeUtc": "2023-09-24T01:00:00Z",
        "rewards": {
            "inventory": [
                {
                    "id": "c409783a-8f32-fa9f-1026-54bbaaaedc38",
                    "amount": 10
                }
            ],
            "buildplates": [],
            "challenges": [],
            "personaItems": [],
            "utilityBlocks": []
        },
        "percentComplete": 0,
        "isComplete": false,
        "state": "Locked",
        "category": "season_17",
        "currentCount": 0,
        "totalThreshold": 2,
        "parentId": "3d82b9c1-f4e0-4a20-b87e-9a11734bcb6a",
        "order": 8,
        "rarity": null,
        "prerequisiteLogicalCondition": "Or",
        "prerequisiteIds": [
            "edf9f1dc-f73f-4275-a3e5-e1d9d861d158",
            "0d4c99ce-b485-4890-be69-caab88400df3"
        ],
        "groupId": "cc456b52-1586-4e75-b7e9-aa811f609567",
        "clientProperties": {}
    },
    "26f62499-5996-477a-8530-22f852b1ccad": {
        "referenceId": "520f09c2-7a83-49f3-b579-654ca2944adb",
        "duration": "Season",
        "type": "Regular",
        "endTimeUtc": "2023-09-24T01:00:00Z",
        "rewards": {
            "inventory": [
                {
                    "id": "3f473106-d0c3-4f44-9db9-ace843e3a11a",
                    "amount": 1
                }
            ],
            "buildplates": [],
            "challenges": [],
            "personaItems": [],
            "utilityBlocks": []
        },
        "percentComplete": 0,
        "isComplete": false,
        "state": "Locked",
        "category": "season_17",
        "currentCount": 0,
        "totalThreshold": 3,
        "parentId": "70961d0f-a762-4846-a79c-576d685263f4",
        "order": 25,
        "rarity": null,
        "prerequisiteLogicalCondition": "Or",
        "prerequisiteIds": [
            "abd271f8-22cd-42ff-b985-cd5e642ea25a",
            "2ebbf878-5c69-4f55-8858-83bd53c57381"
        ],
        "groupId": "cc456b52-1586-4e75-b7e9-aa811f609567",
        "clientProperties": {}
    },
    "29ebe650-072f-4f70-996f-4ffdda93ed1f": {
        "referenceId": "1282801b-a5a8-4339-a587-b16d31468b55",
        "duration": "Season",
        "type": "Regular",
        "endTimeUtc": "2023-09-24T01:00:00Z",
        "rewards": {
            "inventory": [],
            "rubies": 20,
            "buildplates": [],
            "challenges": [],
            "personaItems": [],
            "utilityBlocks": []
        },
        "percentComplete": 0,
        "isComplete": false,
        "state": "Locked",
        "category": "season_17",
        "currentCount": 0,
        "totalThreshold": 2,
        "parentId": "70961d0f-a762-4846-a79c-576d685263f4",
        "order": 24,
        "rarity": null,
        "prerequisiteLogicalCondition": "Or",
        "prerequisiteIds": [
            "dbdb4592-4211-4ffe-a76c-8a3a2213c93c",
            "3183cf3d-19e5-4bf5-b8a8-3085f4e650d3"
        ],
        "groupId": "cc456b52-1586-4e75-b7e9-aa811f609567",
        "clientProperties": {}
    },
    "2bb26b41-e841-4815-ade5-a0d93ac51258": {
        "referenceId": "b8f5af67-ff23-4c1d-95a9-f6ccff5137e0",
        "duration": "Season",
        "type": "Regular",
        "endTimeUtc": "2023-09-24T01:00:00Z",
        "rewards": {
            "inventory": [
                {
                    "id": "179b96c5-7627-406d-e42b-838a29ab0291",
                    "amount": 1
                }
            ],
            "buildplates": [],
            "challenges": [],
            "personaItems": [],
            "utilityBlocks": []
        },
        "percentComplete": 0,
        "isComplete": false,
        "state": "Locked",
        "category": "season_17",
        "currentCount": 0,
        "totalThreshold": 3,
        "parentId": "70961d0f-a762-4846-a79c-576d685263f4",
        "order": 20,
        "rarity": null,
        "prerequisiteLogicalCondition": "Or",
        "prerequisiteIds": [
            "96593763-a8a6-41f8-986a-8d6df0470c57",
            "703c24f0-aedb-4fc1-bce8-6b06229b2006"
        ],
        "groupId": "cc456b52-1586-4e75-b7e9-aa811f609567",
        "clientProperties": {}
    },
    "2ebbf878-5c69-4f55-8858-83bd53c57381": {
        "referenceId": "f3410363-8eee-4c60-8075-d785c02f158e",
        "duration": "Season",
        "type": "Regular",
        "endTimeUtc": "2023-09-24T01:00:00Z",
        "rewards": {
            "inventory": [
                {
                    "id": "1eb4c044-716e-4a26-80f3-be2a7f30fe70",
                    "amount": 1
                }
            ],
            "buildplates": [],
            "challenges": [],
            "personaItems": [],
            "utilityBlocks": []
        },
        "percentComplete": 0,
        "isComplete": false,
        "state": "Locked",
        "category": "season_17",
        "currentCount": 0,
        "totalThreshold": 2,
        "parentId": "70961d0f-a762-4846-a79c-576d685263f4",
        "order": 28,
        "rarity": null,
        "prerequisiteLogicalCondition": "Or",
        "prerequisiteIds": [
            "26f62499-5996-477a-8530-22f852b1ccad",
            "3183cf3d-19e5-4bf5-b8a8-3085f4e650d3",
            "a1394f77-20ba-44c2-b46c-e8d7933f2e51"
        ],
        "groupId": "cc456b52-1586-4e75-b7e9-aa811f609567",
        "clientProperties": {}
    },
    "3183cf3d-19e5-4bf5-b8a8-3085f4e650d3": {
        "referenceId": "857ba971-9a35-4119-b674-9cb12bfd0693",
        "duration": "Season",
        "type": "Regular",
        "endTimeUtc": "2023-09-24T01:00:00Z",
        "rewards": {
            "inventory": [
                {
                    "id": "79a0bc61-84e6-4c77-ba12-df0bad32a06f",
                    "amount": 1
                }
            ],
            "buildplates": [],
            "challenges": [],
            "personaItems": [],
            "utilityBlocks": []
        },
        "percentComplete": 0,
        "isComplete": false,
        "state": "Locked",
        "category": "season_17",
        "currentCount": 0,
        "totalThreshold": 6,
        "parentId": "70961d0f-a762-4846-a79c-576d685263f4",
        "order": 27,
        "rarity": null,
        "prerequisiteLogicalCondition": "Or",
        "prerequisiteIds": [
            "29ebe650-072f-4f70-996f-4ffdda93ed1f",
            "2ebbf878-5c69-4f55-8858-83bd53c57381",
            "ab2e0734-62b1-4383-a239-d1b4d4a93dc4"
        ],
        "groupId": "cc456b52-1586-4e75-b7e9-aa811f609567",
        "clientProperties": {}
    },
    "3d82b9c1-f4e0-4a20-b87e-9a11734bcb6a": {
        "referenceId": "87ded7ff-f837-4a20-bedd-77aa3d60c060",
        "duration": "Season",
        "type": "Regular",
        "endTimeUtc": "2023-09-24T01:00:00Z",
        "rewards": {
            "inventory": [],
            "buildplates": [],
            "challenges": [],
            "personaItems": [],
            "utilityBlocks": []
        },
        "percentComplete": 0,
        "isComplete": false,
        "state": "Active",
        "category": "season_17",
        "currentCount": 0,
        "totalThreshold": 1,
        "parentId": "cc456b52-1586-4e75-b7e9-aa811f609567",
        "order": 0,
        "rarity": null,
        "prerequisiteLogicalCondition": "And",
        "prerequisiteIds": [],
        "groupId": "cc456b52-1586-4e75-b7e9-aa811f609567",
        "clientProperties": {}
    },
    "63e5d65e-1152-4adc-97e7-2818854a867a": {
        "referenceId": "2a599738-efe0-45bf-8fec-1ff73b25f374",
        "duration": "Season",
        "type": "Regular",
        "endTimeUtc": "2023-09-24T01:00:00Z",
        "rewards": {
            "inventory": [
                {
                    "id": "6e0d14d1-d406-4040-8582-3ec3d160079f",
                    "amount": 1
                }
            ],
            "buildplates": [],
            "challenges": [],
            "personaItems": [],
            "utilityBlocks": []
        },
        "percentComplete": 0,
        "isComplete": false,
        "state": "Locked",
        "category": "season_17",
        "currentCount": 0,
        "totalThreshold": 3,
        "parentId": "70961d0f-a762-4846-a79c-576d685263f4",
        "order": 18,
        "rarity": null,
        "prerequisiteLogicalCondition": "Or",
        "prerequisiteIds": [
            "96593763-a8a6-41f8-986a-8d6df0470c57",
            "dbdb4592-4211-4ffe-a76c-8a3a2213c93c"
        ],
        "groupId": "cc456b52-1586-4e75-b7e9-aa811f609567",
        "clientProperties": {}
    },
    "68337eb8-c1c8-4f57-876a-d262ca7a22f4": {
        "referenceId": "59c9d986-e9a0-4651-9575-9ecb30f932e6",
        "duration": "Season",
        "type": "Regular",
        "endTimeUtc": "2023-09-24T01:00:00Z",
        "rewards": {
            "inventory": [
                {
                    "id": "7535d81e-c960-28f2-d3ca-d1fd7b813c34",
                    "amount": 1
                }
            ],
            "buildplates": [],
            "challenges": [],
            "personaItems": [],
            "utilityBlocks": []
        },
        "percentComplete": 0,
        "isComplete": false,
        "state": "Locked",
        "category": "season_17",
        "currentCount": 0,
        "totalThreshold": 1,
        "parentId": "70961d0f-a762-4846-a79c-576d685263f4",
        "order": 26,
        "rarity": null,
        "prerequisiteLogicalCondition": "Or",
        "prerequisiteIds": [
            "703c24f0-aedb-4fc1-bce8-6b06229b2006"
        ],
        "groupId": "cc456b52-1586-4e75-b7e9-aa811f609567",
        "clientProperties": {}
    },
    "703c24f0-aedb-4fc1-bce8-6b06229b2006": {
        "referenceId": "537fec4e-fed5-4618-b7b5-c171434111ad",
        "duration": "Season",
        "type": "Regular",
        "endTimeUtc": "2023-09-24T01:00:00Z",
        "rewards": {
            "inventory": [
                {
                    "id": "75875fbb-9615-41da-9a04-5a1d290513b5",
                    "amount": 2
                }
            ],
            "buildplates": [],
            "challenges": [],
            "personaItems": [],
            "utilityBlocks": []
        },
        "percentComplete": 0,
        "isComplete": false,
        "state": "Locked",
        "category": "season_17",
        "currentCount": 0,
        "totalThreshold": 3,
        "parentId": "70961d0f-a762-4846-a79c-576d685263f4",
        "order": 23,
        "rarity": null,
        "prerequisiteLogicalCondition": "Or",
        "prerequisiteIds": [
            "2bb26b41-e841-4815-ade5-a0d93ac51258",
            "68337eb8-c1c8-4f57-876a-d262ca7a22f4"
        ],
        "groupId": "cc456b52-1586-4e75-b7e9-aa811f609567",
        "clientProperties": {}
    },
    "70961d0f-a762-4846-a79c-576d685263f4": {
        "referenceId": "45262b2b-2f74-4c6a-ae8e-837e81e80c46",
        "duration": "Season",
        "type": "Regular",
        "endTimeUtc": "2023-09-24T01:00:00Z",
        "rewards": {
            "inventory": [],
            "buildplates": [],
            "challenges": [],
            "personaItems": [],
            "utilityBlocks": []
        },
        "percentComplete": 0,
        "isComplete": false,
        "state": "Locked",
        "category": "season_17",
        "currentCount": 0,
        "totalThreshold": 1,
        "parentId": "cc456b52-1586-4e75-b7e9-aa811f609567",
        "order": 0,
        "rarity": null,
        "prerequisiteLogicalCondition": "And",
        "prerequisiteIds": [
            "3d82b9c1-f4e0-4a20-b87e-9a11734bcb6a"
        ],
        "groupId": "cc456b52-1586-4e75-b7e9-aa811f609567",
        "clientProperties": {}
    },
    "761c6754-ad1f-4cbf-a116-8a1e1dd611b9": {
        "referenceId": "09e5a7ea-c4a7-4401-af88-dcb8b4f47abe",
        "duration": "Season",
        "type": "Regular",
        "endTimeUtc": "2023-09-24T01:00:00Z",
        "rewards": {
            "inventory": [
                {
                    "id": "75875fbb-9615-41da-9a04-5a1d290513b5",
                    "amount": 1
                }
            ],
            "buildplates": [],
            "challenges": [],
            "personaItems": [],
            "utilityBlocks": []
        },
        "percentComplete": 0,
        "isComplete": false,
        "state": "Locked",
        "category": "season_17",
        "currentCount": 0,
        "totalThreshold": 8,
        "parentId": "3d82b9c1-f4e0-4a20-b87e-9a11734bcb6a",
        "order": 9,
        "rarity": null,
        "prerequisiteLogicalCondition": "Or",
        "prerequisiteIds": [
            "8e052786-0495-4eb9-80bf-e1b36b9d89ef",
            "fb13127e-d19d-48bd-a977-f830eecff180"
        ],
        "groupId": "cc456b52-1586-4e75-b7e9-aa811f609567",
        "clientProperties": {}
    },
    "8e052786-0495-4eb9-80bf-e1b36b9d89ef": {
        "referenceId": "f022ad24-44e4-484e-b839-77237bb3d1b8",
        "duration": "Season",
        "type": "Regular",
        "endTimeUtc": "2023-09-24T01:00:00Z",
        "rewards": {
            "inventory": [
                {
                    "id": "ea7306fa-9dd0-897f-d1a8-2529521cd5f2",
                    "amount": 1
                }
            ],
            "buildplates": [],
            "challenges": [],
            "personaItems": [],
            "utilityBlocks": []
        },
        "percentComplete": 0,
        "isComplete": false,
        "state": "Locked",
        "category": "season_17",
        "currentCount": 0,
        "totalThreshold": 2,
        "parentId": "3d82b9c1-f4e0-4a20-b87e-9a11734bcb6a",
        "order": 6,
        "rarity": null,
        "prerequisiteLogicalCondition": "Or",
        "prerequisiteIds": [
            "e81d6ce8-622c-461a-a2d5-bb1f8ac36c62",
            "761c6754-ad1f-4cbf-a116-8a1e1dd611b9"
        ],
        "groupId": "cc456b52-1586-4e75-b7e9-aa811f609567",
        "clientProperties": {}
    },
    "8ff3a6c3-0a1a-4343-8672-7f559cc8918d": {
        "referenceId": "d4a304cf-d6b5-4fcd-862c-8d7f3418443e",
        "duration": "Season",
        "type": "Regular",
        "endTimeUtc": "2023-09-24T01:00:00Z",
        "rewards": {
            "inventory": [
                {
                    "id": "c4231eb5-8fc0-4ad7-ad18-ecfcb0734049",
                    "amount": 1
                }
            ],
            "buildplates": [],
            "challenges": [],
            "personaItems": [],
            "utilityBlocks": []
        },
        "percentComplete": 0,
        "isComplete": false,
        "state": "Locked",
        "category": "season_17",
        "currentCount": 0,
        "totalThreshold": 8,
        "parentId": "70961d0f-a762-4846-a79c-576d685263f4",
        "order": 19,
        "rarity": null,
        "prerequisiteLogicalCondition": "Or",
        "prerequisiteIds": [
            "96593763-a8a6-41f8-986a-8d6df0470c57",
            "abd271f8-22cd-42ff-b985-cd5e642ea25a"
        ],
        "groupId": "cc456b52-1586-4e75-b7e9-aa811f609567",
        "clientProperties": {}
    },
    "96593763-a8a6-41f8-986a-8d6df0470c57": {
        "referenceId": "0688f294-76a8-41ed-a34c-24b8d9e3bc98",
        "duration": "Season",
        "type": "Regular",
        "endTimeUtc": "2023-09-24T01:00:00Z",
        "rewards": {
            "experiencePoints": 250,
            "inventory": [],
            "buildplates": [],
            "challenges": [],
            "personaItems": [],
            "utilityBlocks": []
        },
        "percentComplete": 0,
        "isComplete": false,
        "state": "Locked",
        "category": "season_17",
        "currentCount": 0,
        "totalThreshold": 1,
        "parentId": "70961d0f-a762-4846-a79c-576d685263f4",
        "order": 16,
        "rarity": null,
        "prerequisiteLogicalCondition": "Or",
        "prerequisiteIds": [
            "fb13127e-d19d-48bd-a977-f830eecff180"
        ],
        "groupId": "cc456b52-1586-4e75-b7e9-aa811f609567",
        "clientProperties": {}
    },
    "a1394f77-20ba-44c2-b46c-e8d7933f2e51": {
        "referenceId": "290e2c67-cdfb-4aab-8fae-f8b910a096c0",
        "duration": "Season",
        "type": "Regular",
        "endTimeUtc": "2023-09-24T01:00:00Z",
        "rewards": {
            "inventory": [],
            "buildplates": [],
            "challenges": [],
            "personaItems": [
                "d7725840-4376-44fc-9220-585f45775371"
            ],
            "utilityBlocks": []
        },
        "percentComplete": 0,
        "isComplete": false,
        "state": "Locked",
        "category": "season_17",
        "currentCount": 0,
        "totalThreshold": 3,
        "parentId": "70961d0f-a762-4846-a79c-576d685263f4",
        "order": 29,
        "rarity": null,
        "prerequisiteLogicalCondition": "Or",
        "prerequisiteIds": [
            "2ebbf878-5c69-4f55-8858-83bd53c57381",
            "0b346237-79a4-4f24-a45a-e2e6284e3e56"
        ],
        "groupId": "cc456b52-1586-4e75-b7e9-aa811f609567",
        "clientProperties": {}
    },
    "ab2e0734-62b1-4383-a239-d1b4d4a93dc4": {
        "referenceId": "1d4197fc-49b7-4cca-8397-8792aba78037",
        "duration": "Season",
        "type": "Regular",
        "endTimeUtc": "2023-09-24T01:00:00Z",
        "rewards": {
            "inventory": [
                {
                    "id": "408bb17b-da92-0cea-7496-ee01c6a542d7",
                    "amount": 5
                }
            ],
            "buildplates": [],
            "challenges": [],
            "personaItems": [],
            "utilityBlocks": []
        },
        "percentComplete": 0,
        "isComplete": false,
        "state": "Locked",
        "category": "season_17",
        "currentCount": 0,
        "totalThreshold": 1,
        "parentId": "70961d0f-a762-4846-a79c-576d685263f4",
        "order": 30,
        "rarity": null,
        "prerequisiteLogicalCondition": "Or",
        "prerequisiteIds": [
            "3183cf3d-19e5-4bf5-b8a8-3085f4e650d3",
            "152a875e-2389-4245-8e1f-08f1915d6c7a"
        ],
        "groupId": "cc456b52-1586-4e75-b7e9-aa811f609567",
        "clientProperties": {}
    },
    "abd271f8-22cd-42ff-b985-cd5e642ea25a": {
        "referenceId": "5a6fbcce-2d2d-47f9-b36e-4d34c351f8a3",
        "duration": "Season",
        "type": "Regular",
        "endTimeUtc": "2023-09-24T01:00:00Z",
        "rewards": {
            "inventory": [
                {
                    "id": "123dd088-ba76-71ce-b40c-2f05b948f303",
                    "amount": 1
                }
            ],
            "buildplates": [],
            "challenges": [],
            "personaItems": [],
            "utilityBlocks": []
        },
        "percentComplete": 0,
        "isComplete": false,
        "state": "Locked",
        "category": "season_17",
        "currentCount": 0,
        "totalThreshold": 2,
        "parentId": "70961d0f-a762-4846-a79c-576d685263f4",
        "order": 22,
        "rarity": null,
        "prerequisiteLogicalCondition": "Or",
        "prerequisiteIds": [
            "8ff3a6c3-0a1a-4343-8672-7f559cc8918d",
            "dbdb4592-4211-4ffe-a76c-8a3a2213c93c",
            "26f62499-5996-477a-8530-22f852b1ccad"
        ],
        "groupId": "cc456b52-1586-4e75-b7e9-aa811f609567",
        "clientProperties": {}
    },
    "cc456b52-1586-4e75-b7e9-aa811f609567": {
        "referenceId": "a46e0e1e-51cd-4fbc-b3b2-f6d33c78532c",
        "duration": "Season",
        "type": "Regular",
        "endTimeUtc": "2023-09-24T01:00:00Z",
        "rewards": {
            "inventory": [],
            "buildplates": [],
            "challenges": [],
            "personaItems": [],
            "utilityBlocks": []
        },
        "percentComplete": 0,
        "isComplete": false,
        "state": "Active",
        "category": "season_17",
        "currentCount": 0,
        "totalThreshold": 1,
        "parentId": null,
        "order": 0,
        "rarity": null,
        "prerequisiteLogicalCondition": "And",
        "prerequisiteIds": [],
        "groupId": "cc456b52-1586-4e75-b7e9-aa811f609567",
        "clientProperties": {}
    },
    "d434c853-7fab-4ac7-b64b-80fb6dd9ddd9": {
        "referenceId": "e02e6a5d-8541-4d34-a2ee-9bcdcc3381a4",
        "duration": "Season",
        "type": "Regular",
        "endTimeUtc": "2023-09-24T01:00:00Z",
        "rewards": {
            "inventory": [
                {
                    "id": "0d494b76-58cd-4744-aa3e-affe0e4ebb87",
                    "amount": 1
                }
            ],
            "buildplates": [],
            "challenges": [],
            "personaItems": [],
            "utilityBlocks": []
        },
        "percentComplete": 0,
        "isComplete": false,
        "state": "Locked",
        "category": "season_17",
        "currentCount": 0,
        "totalThreshold": 1,
        "parentId": "3d82b9c1-f4e0-4a20-b87e-9a11734bcb6a",
        "order": 10,
        "rarity": null,
        "prerequisiteLogicalCondition": "Or",
        "prerequisiteIds": [
            "d8d2abd5-f318-4c34-a257-57b9691a2774",
            "fb13127e-d19d-48bd-a977-f830eecff180"
        ],
        "groupId": "cc456b52-1586-4e75-b7e9-aa811f609567",
        "clientProperties": {}
    },
    "d8d2abd5-f318-4c34-a257-57b9691a2774": {
        "referenceId": "bfd00245-4801-46fa-af1a-28979df23377",
        "duration": "Season",
        "type": "Regular",
        "endTimeUtc": "2023-09-24T01:00:00Z",
        "rewards": {
            "inventory": [
                {
                    "id": "c14fc209-f280-bccc-bb4a-6c2a3fc71abc",
                    "amount": 3
                }
            ],
            "buildplates": [],
            "challenges": [],
            "personaItems": [],
            "utilityBlocks": []
        },
        "percentComplete": 0,
        "isComplete": false,
        "state": "Locked",
        "category": "season_17",
        "currentCount": 0,
        "totalThreshold": 2,
        "parentId": "3d82b9c1-f4e0-4a20-b87e-9a11734bcb6a",
        "order": 7,
        "rarity": null,
        "prerequisiteLogicalCondition": "Or",
        "prerequisiteIds": [
            "1ccbe15e-3a72-4d8b-ad0b-00a23ac72eb1",
            "d434c853-7fab-4ac7-b64b-80fb6dd9ddd9"
        ],
        "groupId": "cc456b52-1586-4e75-b7e9-aa811f609567",
        "clientProperties": {}
    },
    "dbdb4592-4211-4ffe-a76c-8a3a2213c93c": {
        "referenceId": "af6d9346-4a07-458e-880e-3f3601db8739",
        "duration": "Season",
        "type": "Regular",
        "endTimeUtc": "2023-09-24T01:00:00Z",
        "rewards": {
            "inventory": [
                {
                    "id": "ff723264-b108-9d24-f445-73a3322fc72e",
                    "amount": 1
                }
            ],
            "buildplates": [],
            "challenges": [],
            "personaItems": [],
            "utilityBlocks": []
        },
        "percentComplete": 0,
        "isComplete": false,
        "state": "Locked",
        "category": "season_17",
        "currentCount": 0,
        "totalThreshold": 7,
        "parentId": "70961d0f-a762-4846-a79c-576d685263f4",
        "order": 21,
        "rarity": null,
        "prerequisiteLogicalCondition": "Or",
        "prerequisiteIds": [
            "63e5d65e-1152-4adc-97e7-2818854a867a",
            "abd271f8-22cd-42ff-b985-cd5e642ea25a",
            "29ebe650-072f-4f70-996f-4ffdda93ed1f"
        ],
        "groupId": "cc456b52-1586-4e75-b7e9-aa811f609567",
        "clientProperties": {}
    },
    "e81d6ce8-622c-461a-a2d5-bb1f8ac36c62": {
        "referenceId": "10986d40-516f-459b-bc24-a16296998c1e",
        "duration": "Season",
        "type": "Regular",
        "endTimeUtc": "2023-09-24T01:00:00Z",
        "rewards": {
            "inventory": [
                {
                    "id": "eea50131-e909-214f-d5d2-e8b83febe31a",
                    "amount": 30
                }
            ],
            "buildplates": [],
            "challenges": [],
            "personaItems": [],
            "utilityBlocks": []
        },
        "percentComplete": 0,
        "isComplete": false,
        "state": "Locked",
        "category": "season_17",
        "currentCount": 0,
        "totalThreshold": 2,
        "parentId": "3d82b9c1-f4e0-4a20-b87e-9a11734bcb6a",
        "order": 3,
        "rarity": null,
        "prerequisiteLogicalCondition": "Or",
        "prerequisiteIds": [
            "f0532069-a70a-4a01-8611-f770bb46d9cd",
            "8e052786-0495-4eb9-80bf-e1b36b9d89ef"
        ],
        "groupId": "cc456b52-1586-4e75-b7e9-aa811f609567",
        "clientProperties": {}
    },
    "edf9f1dc-f73f-4275-a3e5-e1d9d861d158": {
        "referenceId": "d58c96b5-6962-4a97-a4e1-45d635e8cef2",
        "duration": "Season",
        "type": "Regular",
        "endTimeUtc": "2023-09-24T01:00:00Z",
        "rewards": {
            "inventory": [
                {
                    "id": "8864e97c-36a6-b66b-63c6-5b247cdd1aaa",
                    "amount": 8
                }
            ],
            "buildplates": [],
            "challenges": [],
            "personaItems": [],
            "utilityBlocks": []
        },
        "percentComplete": 0,
        "isComplete": false,
        "state": "Locked",
        "category": "season_17",
        "currentCount": 0,
        "totalThreshold": 3,
        "parentId": "3d82b9c1-f4e0-4a20-b87e-9a11734bcb6a",
        "order": 5,
        "rarity": null,
        "prerequisiteLogicalCondition": "Or",
        "prerequisiteIds": [
            "f0532069-a70a-4a01-8611-f770bb46d9cd",
            "242b6706-8112-4302-819b-bde98fd574f6"
        ],
        "groupId": "cc456b52-1586-4e75-b7e9-aa811f609567",
        "clientProperties": {}
    },
    "f0532069-a70a-4a01-8611-f770bb46d9cd": {
        "referenceId": "a7ac0df7-4239-491d-9dc4-8691d053ebf4",
        "duration": "Season",
        "type": "Regular",
        "endTimeUtc": "2023-09-24T01:00:00Z",
        "rewards": {
            "inventory": [
                {
                    "id": "d9bbd707-8a7a-4edb-a85c-f8ec0c78a1f9",
                    "amount": 1
                }
            ],
            "buildplates": [],
            "challenges": [],
            "personaItems": [],
            "utilityBlocks": []
        },
        "percentComplete": 100,
        "isComplete": true,
        "state": "Completed",
        "category": "season_17",
        "currentCount": 1,
        "totalThreshold": 1,
        "parentId": "3d82b9c1-f4e0-4a20-b87e-9a11734bcb6a",
        "order": 1,
        "rarity": null,
        "prerequisiteLogicalCondition": "And",
        "prerequisiteIds": [],
        "groupId": "cc456b52-1586-4e75-b7e9-aa811f609567",
        "clientProperties": {}
    },
    "fb13127e-d19d-48bd-a977-f830eecff180": {
        "referenceId": "92ed3b3a-a132-44c8-8045-2d6d828c0177",
        "duration": "Season",
        "type": "Regular",
        "endTimeUtc": "2023-09-24T01:00:00Z",
        "rewards": {
            "inventory": [
                {
                    "id": "81a84b7e-928f-7157-254c-6543e90dbc59",
                    "amount": 1
                }
            ],
            "buildplates": [],
            "challenges": [],
            "personaItems": [],
            "utilityBlocks": []
        },
        "percentComplete": 0,
        "isComplete": false,
        "state": "Locked",
        "category": "season_17",
        "currentCount": 0,
        "totalThreshold": 4,
        "parentId": "3d82b9c1-f4e0-4a20-b87e-9a11734bcb6a",
        "order": 13,
        "rarity": null,
        "prerequisiteLogicalCondition": "Or",
        "prerequisiteIds": [
            "761c6754-ad1f-4cbf-a116-8a1e1dd611b9",
            "d434c853-7fab-4ac7-b64b-80fb6dd9ddd9",
            "0d4c99ce-b485-4890-be69-caab88400df3"
        ],
        "groupId": "cc456b52-1586-4e75-b7e9-aa811f609567",
        "clientProperties": {}
    },
    "ccc1836f-ccda-4f33-8a4d-c42d8d366255": {
        "referenceId": "ccc1836f-ccda-4f33-8a4d-c42d8d366255",
        "duration": "PersonalTimed",
        "type": "Regular",
        "endTimeUtc": "2022-12-17T00:00:00Z",
        "rewards": {
            "experiencePoints": 75,
            "inventory": [],
            "buildplates": [],
            "challenges": [],
            "personaItems": [],
            "utilityBlocks": []
        },
        "percentComplete": 0,
        "isComplete": false,
        "state": "Active",
        "category": "Collection",
        "currentCount": 0,
        "totalThreshold": 5,
        "parentId": null,
        "order": 0,
        "rarity": null,
        "prerequisiteLogicalCondition": "And",
        "prerequisiteIds": [],
        "groupId": null,
        "clientProperties": {}
    },
    "91d23ab8-7a63-4d6d-8b4b-6ae462a51067": {
        "referenceId": "91d23ab8-7a63-4d6d-8b4b-6ae462a51067",
        "duration": "PersonalTimed",
        "type": "Regular",
        "endTimeUtc": "2022-12-17T00:00:00Z",
        "rewards": {
            "experiencePoints": 30,
            "inventory": [],
            "buildplates": [],
            "challenges": [],
            "personaItems": [],
            "utilityBlocks": []
        },
        "percentComplete": 0,
        "isComplete": false,
        "state": "Active",
        "category": "sheep",
        "currentCount": 0,
        "totalThreshold": 1,
        "parentId": null,
        "order": 0,
        "rarity": "Common",
        "prerequisiteLogicalCondition": "And",
        "prerequisiteIds": [],
        "groupId": null,
        "clientProperties": {}
    }
}
""";

    [HttpPost]
    public Task<Results<ContentHttpResult, BadRequest>> Post(CancellationToken cancellationToken)
        => Get(cancellationToken);
}
