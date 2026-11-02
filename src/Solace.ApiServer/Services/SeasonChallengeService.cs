using Microsoft.EntityFrameworkCore;
using Solace.ApiServer.Types.Challenges;
using Solace.ApiServer.Utils;
using Solace.Db.Earth;
using Solace.Db.Earth.Models.Challenges;
using Solace.Db.Earth.Models.Player;
using Solace.EventBus.Client;
using Solace.ObjectStore.Client;
using Solace.StaticData;

namespace Solace.ApiServer.Services;

internal sealed partial class SeasonChallengeService : IDisposable
{
    private static readonly Guid SeasonCompleteSlotId = Guid.Parse("a46e0e1e-51cd-4fbc-b3b2-f6d33c78532c");
    private static readonly Guid SeasonCustomizationSlotId = Guid.Parse("45262b2b-2f74-4c6a-ae8e-837e81e80c46");
    private static readonly Guid SeasonSuperPrizeSlotId = Guid.Parse("87ded7ff-f837-4a20-bedd-77aa3d60c060");

    private sealed record CachedSeason(
        SeasonEF Season,
        List<ChallengeDefinitionEF> Challenges
    );

    private readonly ObjectStoreClient _objectStore;
    private readonly StaticDataProvider _staticData;
    private readonly ILogger<SeasonChallengeService> _logger;

    private CachedSeason? _cachedSeason;
    private readonly SemaphoreSlim _seasonLock = new(1, 1);
    private Subscriber? _subscriber;

    public SeasonChallengeService(ObjectStoreClient objectStore, StaticDataProvider staticData, ILogger<SeasonChallengeService> logger)
    {
        _objectStore = objectStore;
        _staticData = staticData;
        _logger = logger;
    }

    public async Task InitializeAsync(EventBusClient eventBusClient)
        => _subscriber = await eventBusClient.AddSubscriberAsync(
            "seasons",
            HandleEvent,
            exception =>
            {
                if (exception is not null)
                {
                    LogSubscriberError(_logger, exception);
                }

                return Task.CompletedTask;
            });

    private Task HandleEvent(SubscriberEvent ev, CancellationToken cancellationToken)
    {
        if (ev.Type is "seasons_data_updated")
        {
            LogSeasonsDataUpdated(_logger);
            _cachedSeason = null;
        }

        return Task.CompletedTask;
    }

    private async Task<CachedSeason?> GetActiveSeasonAsync(EarthDbContext earthDb, CancellationToken cancellationToken)
    {
        var cached = _cachedSeason;
        if (cached is not null)
        {
            return cached;
        }

        await _seasonLock.WaitAsync(cancellationToken);

        try
        {
            if (_cachedSeason is not null)
            {
                return _cachedSeason;
            }

            var season = await earthDb.Seasons
                .AsNoTracking()
                .Include(season => season.Challenges)
                .FirstOrDefaultAsync(s => s.IsActive, cancellationToken);

            if (season is not null)
            {
                _cachedSeason = new CachedSeason(season, [.. season.Challenges.OrderBy(c => c.Order)]);
            }

            return _cachedSeason;
        }
        finally
        {
            _seasonLock.Release();
        }
    }

    public async Task<ChallengesResponse> GetChallengesResponseAsync(EarthDbContext earthDb, Guid profileId, CancellationToken cancellationToken = default)
    {
        var activeSeason = await GetActiveSeasonAsync(earthDb, cancellationToken);
        if (activeSeason is null)
        {
            return new ChallengesResponse([], Guid.Empty);
        }

        var seasonProgress = await earthDb.PlayerSeasonProgresses
            .AsTracking()
            .FirstOrDefaultAsync(progress => progress.ProfileId == profileId && progress.SeasonId == activeSeason.Season.Id, cancellationToken);

        if (seasonProgress is null)
        {
            seasonProgress = new PlayerSeasonProgressEF()
            {
                ProfileId = profileId,
                SeasonId = activeSeason.Season.Id,
                StartedAt = DateTimeOffset.UtcNow,
                ActiveSeasonChallengeId = null,
                IsCompleted = false,
            };

            earthDb.PlayerSeasonProgresses.Add(seasonProgress);

            var hasEverSeenSeasons = await earthDb.PlayerSeasonProgresses.AnyAsync(progress => progress.ProfileId == profileId, cancellationToken);
            if (!hasEverSeenSeasons)
            {
                earthDb.Tokens.Add(new FTUESeasonsTokenEF(profileId, activeSeason.Season.Id));
            }

            earthDb.Tokens.Add(new NewSeasonNotificationTokenEF(profileId, activeSeason.Season.Id));
            await earthDb.SaveChangesAsync(cancellationToken);
        }

        var challengeIds = activeSeason.Challenges.Select(challenge => challenge.Id).ToHashSet();
        var progressByChallengeId = await earthDb.PlayerChallengeProgresses
            .AsTracking()
            .Where(progress => progress.ProfileId == profileId && challengeIds.Contains(progress.ChallengeId))
            .ToDictionaryAsync(p => p.ChallengeId, cancellationToken);

        var challengeDefs = activeSeason.Challenges.ToDictionary(c => c.Id);

        var now = DateTimeOffset.UtcNow;
        var capstoneCompleted = await CompleteCapstoneIfEligibleAsync(
            earthDb,
            activeSeason,
            profileId,
            progressByChallengeId,
            challengeDefs,
            seasonProgress,
            ResultsEF.Builder.Null,
            now,
            cancellationToken);

        if (capstoneCompleted)
        {
            await earthDb.SaveChangesAsync(cancellationToken);
        }

        var challengeRecords = new Dictionary<Guid, ChallengeRecord>(activeSeason.Challenges.Count);

        var category = activeSeason.Season.Key;
        var endTimeUtc = TimeFormatter.FormatTime(activeSeason.Season.EndDate);
        var groupId = activeSeason.Season.Id.ToString();

        // The client expects exactly 2 exclusive rewards in #exclusive_rewards_grid (2x1 grid)
        // Challenges with clientProperties["rewardSparkles"] == "true" are populated into exclusiveRewards.
        // The first exclusive reward is the intermediate milestone (e.g. Persona item),
        // and the second exclusive reward is the capstone / super prize milestone.
        var capstoneChallenge = activeSeason.Challenges
            .LastOrDefault(c => c.IsFinalSeasonChallenge || c.ObjectiveType is ObjectiveTypeEF.SeasonCapstone)
            ?? activeSeason.Challenges.LastOrDefault();

        var milestoneChallenge = activeSeason.Challenges
            .FirstOrDefault(c => c != capstoneChallenge && c.Rewards.PersonaItems.Length > 0)
            ?? (activeSeason.Challenges.Count >= 2
                ? (activeSeason.Challenges.Count >= 3 ? activeSeason.Challenges[^2] : activeSeason.Challenges[0])
                : null);

        HashSet<Guid> sparkleChallengeIds = [];
        if (milestoneChallenge is not null)
        {
            sparkleChallengeIds.Add(milestoneChallenge.Id);
        }

        if (capstoneChallenge is not null)
        {
            sparkleChallengeIds.Add(capstoneChallenge.Id);
        }

        foreach (var challenge in activeSeason.Challenges)
        {
            progressByChallengeId.TryGetValue(challenge.Id, out var progress);
            var state = EvaluateChallengeState(challenge, progressByChallengeId, challengeDefs);
            var currentCount = progress?.CurrentCount ?? 0;
            var isComplete = state is ChallengeStateEF.Completed;
            var percentComplete = challenge.TotalThreshold > 0
                ? int.Clamp(currentCount * 100 / challenge.TotalThreshold, 0, 100)
                : 0;

            var isFinal = challenge.IsFinalSeasonChallenge || challenge.ObjectiveType is ObjectiveTypeEF.SeasonCapstone;
            var clientProperties = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["isFinalSeasonChallenge"] = isFinal ? "1" : "0"
            };

            if (sparkleChallengeIds.Contains(challenge.Id))
            {
                clientProperties["rewardSparkles"] = "true";
            }

            var record = new ChallengeRecord(
                challenge.Id.ToString(),
                challenge.ParentId?.ToString(),
                groupId,
                ChallengeDuration.Season,
                ChallengeType.Regular,
                category,
                null,
                challenge.Order,
                endTimeUtc,
                ChallengeState.FromDb(state),
                isComplete,
                percentComplete,
                currentCount,
                challenge.TotalThreshold,
                [.. challenge.PrerequisiteIds.Select(id => id.ToString())],
                Types.Challenges.LogicalCondition.FromDb(challenge.PrerequisiteLogicalCondition),
                Rewards.FromDBRewardsModel(challenge.Rewards).ToApiResponse(),
                clientProperties
            );

            challengeRecords[challenge.Id] = record;
        }

        if (!challengeRecords.Values.Any(c => c.Order == 0))
        {
            var slot0Id = SeasonCompleteSlotId;
            var slot1Id = SeasonCustomizationSlotId;
            var slot2Id = SeasonSuperPrizeSlotId;

            var emptyRewards = new Rewards().ToApiResponse();

            var slot0Rewards = activeSeason.Season.ExclusiveRewards is not null
                ? Rewards.FromDBRewardsModel(activeSeason.Season.ExclusiveRewards).ToApiResponse()
                : (activeSeason.Challenges.FirstOrDefault(c => c.ObjectiveType == ObjectiveTypeEF.SeasonCapstone) is { } capstone
                    ? Rewards.FromDBRewardsModel(capstone.Rewards).ToApiResponse()
                    : emptyRewards);

            var clientProperties = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["isFinalSeasonChallenge"] = "0"
            };

            // Slot 0: Root anchor
            challengeRecords[slot0Id] = new ChallengeRecord(
                slot0Id.ToString(),
                null,
                groupId,
                ChallengeDuration.Season,
                ChallengeType.Regular,
                category,
                null,
                0,
                endTimeUtc,
                seasonProgress.IsCompleted ? ChallengeState.Completed : ChallengeState.Active,
                seasonProgress.IsCompleted,
                seasonProgress.IsCompleted ? 100 : 0,
                seasonProgress.IsCompleted ? 1 : 0,
                1,
                [],
                Types.Challenges.LogicalCondition.And,
                slot0Rewards,
                clientProperties
            );

            // Slot 1: Character customization milestone anchor
            challengeRecords[slot1Id] = new ChallengeRecord(
                slot1Id.ToString(),
                slot0Id.ToString(),
                groupId,
                ChallengeDuration.Season,
                ChallengeType.Regular,
                category,
                null,
                0,
                endTimeUtc,
                ChallengeState.Active,
                false,
                0,
                0,
                1,
                [slot0Id.ToString()],
                Types.Challenges.LogicalCondition.And,
                emptyRewards,
                clientProperties
            );

            // Slot 2: Super prize milestone anchor
            challengeRecords[slot2Id] = new ChallengeRecord(
                slot2Id.ToString(),
                slot0Id.ToString(),
                groupId,
                ChallengeDuration.Season,
                ChallengeType.Regular,
                category,
                null,
                0,
                endTimeUtc,
                ChallengeState.Active,
                false,
                0,
                0,
                1,
                [],
                Types.Challenges.LogicalCondition.And,
                emptyRewards,
                clientProperties
            );
        }

        return new ChallengesResponse(challengeRecords, seasonProgress.ActiveSeasonChallengeId ?? Guid.Empty);
    }

    public async Task<bool> SetActiveSeasonChallengeAsync(EarthDbContext earthDb, Guid profileId, Guid challengeId, ResultsEF.Builder results, CancellationToken cancellationToken = default)
    {
        var activeSeason = await GetActiveSeasonAsync(earthDb, cancellationToken);
        if (activeSeason is null)
        {
            return false;
        }

        if (challengeId != Guid.Empty && !activeSeason.Challenges.Any(challenge => challenge.Id == challengeId))
        {
            return false;
        }

        var seasonProgress = await earthDb.PlayerSeasonProgresses
            .AsTracking()
            .FirstOrDefaultAsync(progress => progress.ProfileId == profileId && progress.SeasonId == activeSeason.Season.Id, cancellationToken);

        var isFirstSelection = false;
        if (seasonProgress is null)
        {
            isFirstSelection = true;
            seasonProgress = new PlayerSeasonProgressEF
            {
                ProfileId = profileId,
                SeasonId = activeSeason.Season.Id,
                StartedAt = DateTimeOffset.UtcNow,
                ActiveSeasonChallengeId = challengeId == Guid.Empty ? null : challengeId,
                IsCompleted = false,
            };

            earthDb.PlayerSeasonProgresses.Add(seasonProgress);
        }
        else
        {
            if (seasonProgress.ActiveSeasonChallengeId is null && challengeId != Guid.Empty)
            {
                isFirstSelection = true;
            }

            seasonProgress.ActiveSeasonChallengeId = challengeId == Guid.Empty ? null : challengeId;
        }

        if (isFirstSelection && challengeId != Guid.Empty)
        {
            var alreadyHadOOBE = await earthDb.Tokens
                .OfType<OOBESeasonChallengeSelectedTokenEF>()
                .AnyAsync(token => token.ProfileId == profileId, cancellationToken);

            if (!alreadyHadOOBE)
            {
                await TokenUtils.AddTokenAsync(earthDb, results, new OOBESeasonChallengeSelectedTokenEF(profileId), cancellationToken);
            }
        }

        var challengeIds = activeSeason.Challenges.Select(challenge => challenge.Id).ToHashSet();
        var progressByChallengeId = await earthDb.PlayerChallengeProgresses
            .AsTracking()
            .Where(progress => progress.ProfileId == profileId && challengeIds.Contains(progress.ChallengeId))
            .ToDictionaryAsync(p => p.ChallengeId, cancellationToken);

        var challengeDefinitions = activeSeason.Challenges.ToDictionary(challenge => challenge.Id);
        var now = DateTimeOffset.UtcNow;

        var capstoneCompleted = await CompleteCapstoneIfEligibleAsync(
            earthDb,
            activeSeason,
            profileId,
            progressByChallengeId,
            challengeDefinitions,
            seasonProgress,
            results,
            now,
            cancellationToken);

        if (capstoneCompleted)
        {
            results.Tokens();
        }

        results.Challenges();
        await earthDb.SaveChangesAsync(cancellationToken);

        return true;
    }

    public async Task TrackObjectiveProgressAsync(EarthDbContext earthDb, Guid profileId, ObjectiveTypeEF objectiveType, Guid? targetId, int amount, ResultsEF.Builder results, CancellationToken cancellationToken = default)
    {
        if (amount <= 0)
        {
            return;
        }

        var activeSeason = await GetActiveSeasonAsync(earthDb, cancellationToken);
        if (activeSeason is null)
        {
            return;
        }

        var seasonProgress = await earthDb.PlayerSeasonProgresses
            .AsTracking()
            .FirstOrDefaultAsync(progress => progress.ProfileId == profileId && progress.SeasonId == activeSeason.Season.Id, cancellationToken);

        if (seasonProgress is null || seasonProgress.ActiveSeasonChallengeId is null || seasonProgress.ActiveSeasonChallengeId == Guid.Empty)
        {
            return;
        }

        var activeChallengeId = seasonProgress.ActiveSeasonChallengeId.Value;
        var matchingChallenges = activeSeason.Challenges
            .Where(challenge => challenge.Id == activeChallengeId && challenge.ObjectiveType == objectiveType && (challenge.ObjectiveTarget == null || challenge.ObjectiveTarget == targetId))
            .ToList();

        if (matchingChallenges.Count is 0)
        {
            return;
        }

        var challengeIds = activeSeason.Challenges.Select(challenge => challenge.Id).ToHashSet();
        var progressByChallengeId = await earthDb.PlayerChallengeProgresses
            .AsTracking()
            .Where(progress => progress.ProfileId == profileId && challengeIds.Contains(progress.ChallengeId))
            .ToDictionaryAsync(progress => progress.ChallengeId, cancellationToken);

        var challengeDefs = activeSeason.Challenges.ToDictionary(challenge => challenge.Id);
        var anyUpdated = false;
        var now = DateTimeOffset.UtcNow;

        foreach (var challenge in matchingChallenges)
        {
            var state = EvaluateChallengeState(challenge, progressByChallengeId, challengeDefs);
            if (state != ChallengeStateEF.Active)
            {
                continue;
            }

            if (!progressByChallengeId.TryGetValue(challenge.Id, out var progress))
            {
                progress = new PlayerChallengeProgressEF
                {
                    ProfileId = profileId,
                    ChallengeId = challenge.Id,
                    CurrentCount = 0,
                    State = ChallengeStateEF.Active,
                    IsComplete = false,
                };

                earthDb.PlayerChallengeProgresses.Add(progress);
                progressByChallengeId[challenge.Id] = progress;
            }

            if (progress.IsComplete)
            {
                continue;
            }

            var previousCount = progress.CurrentCount;
            progress.CurrentCount += amount;
            anyUpdated = true;

            if (progress.CurrentCount >= challenge.TotalThreshold)
            {
                progress.IsComplete = true;
                progress.State = ChallengeStateEF.Completed;
                progress.CompletedAt = now;
                seasonProgress.ActiveSeasonChallengeId = null;

                var rewards = Rewards.FromDBRewardsModel(challenge.Rewards);
                await rewards.ToRedeemQueryAsync(earthDb, results, _objectStore, profileId, now, _staticData, cancellationToken);

                await ActivityLogUtils.AddEntryAsync(
                    earthDb,
                    results,
                    profileId,
                    new ChallengeCompletedEntryEF(
                        profileId,
                        now,
                        challenge.Rewards,
                        challenge.Id,
                        ChallengeDurationEF.Season,
                        challenge.Order,
                        challenge.IsFinalSeasonChallenge
                    ),
                    cancellationToken);

                if (challenge.IsFinalSeasonChallenge)
                {
                    seasonProgress.IsCompleted = true;
                    seasonProgress.CompletedAt = now;
                }

                earthDb.Tokens.Add(new ChallengeProgressTokenEF(profileId, challenge.Id, previousCount, progress.CurrentCount, challenge.TotalThreshold, ChallengeCategoryEF.Season));
                earthDb.Tokens.Add(new ChallengeCompleteTokenEF(profileId, challenge.Id, activeSeason.Season.EndDate));
            }
            else
            {
                earthDb.Tokens.Add(new ChallengeProgressTokenEF(profileId, challenge.Id, previousCount, progress.CurrentCount, challenge.TotalThreshold, ChallengeCategoryEF.Season));
            }
        }

        if (anyUpdated)
        {
            await CompleteCapstoneIfEligibleAsync(
                earthDb,
                activeSeason,
                profileId,
                progressByChallengeId,
                challengeDefs,
                seasonProgress,
                results,
                now,
                cancellationToken);

            results.Challenges();
            results.Tokens();

            await earthDb.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task<bool> CompleteCapstoneIfEligibleAsync(EarthDbContext earthDb, CachedSeason activeSeason, Guid profileId, Dictionary<Guid, PlayerChallengeProgressEF> progressByChallengeId, Dictionary<Guid, ChallengeDefinitionEF> challengeDefs, PlayerSeasonProgressEF? seasonProgress, ResultsEF.Builder results, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var capstones = activeSeason.Challenges
            .Where(challenge => challenge.ObjectiveType is ObjectiveTypeEF.SeasonCapstone)
            .ToList();

        if (capstones.Count is 0)
        {
            return false;
        }

        var anyCompleted = false;
        bool completedInPass;
        do
        {
            completedInPass = false;
            foreach (var challenge in capstones)
            {
                if (progressByChallengeId.TryGetValue(challenge.Id, out var existingProgress) && existingProgress.IsComplete)
                {
                    continue;
                }

                var state = EvaluateChallengeState(challenge, progressByChallengeId, challengeDefs);
                if (state is not ChallengeStateEF.Active)
                {
                    continue;
                }

                if (!progressByChallengeId.TryGetValue(challenge.Id, out var progress))
                {
                    progress = new PlayerChallengeProgressEF
                    {
                        ProfileId = profileId,
                        ChallengeId = challenge.Id,
                        CurrentCount = 0,
                        State = ChallengeStateEF.Active,
                        IsComplete = false,
                    };

                    earthDb.PlayerChallengeProgresses.Add(progress);
                    progressByChallengeId[challenge.Id] = progress;
                }

                var previousCount = progress.CurrentCount;
                progress.CurrentCount = challenge.TotalThreshold;
                progress.IsComplete = true;
                progress.State = ChallengeStateEF.Completed;
                progress.CompletedAt = now;

                var rewards = Rewards.FromDBRewardsModel(challenge.Rewards);
                await rewards.ToRedeemQueryAsync(earthDb, results, _objectStore, profileId, now, _staticData, cancellationToken);

                await ActivityLogUtils.AddEntryAsync(
                    earthDb,
                    results,
                    profileId,
                    new ChallengeCompletedEntryEF(
                        profileId,
                        now,
                        challenge.Rewards,
                        challenge.Id,
                        ChallengeDurationEF.Season,
                        challenge.Order,
                        challenge.IsFinalSeasonChallenge || challenge.ObjectiveType is ObjectiveTypeEF.SeasonCapstone
                    ),
                    cancellationToken);

                if (challenge.IsFinalSeasonChallenge || challenge.ObjectiveType is ObjectiveTypeEF.SeasonCapstone)
                {
                    if (seasonProgress is not null)
                    {
                        seasonProgress.IsCompleted = true;
                        seasonProgress.CompletedAt = now;
                    }
                }

                earthDb.Tokens.Add(new ChallengeProgressTokenEF(profileId, challenge.Id, previousCount, progress.CurrentCount, challenge.TotalThreshold, ChallengeCategoryEF.Season));
                earthDb.Tokens.Add(new ChallengeCompleteTokenEF(profileId, challenge.Id, activeSeason.Season.EndDate));

                completedInPass = true;
                anyCompleted = true;
            }
        } while (completedInPass);

        return anyCompleted;
    }

    private static ChallengeStateEF EvaluateChallengeState(ChallengeDefinitionEF challenge, Dictionary<Guid, PlayerChallengeProgressEF> progressByChallengeId, Dictionary<Guid, ChallengeDefinitionEF> allChallenges)
    {
        if (progressByChallengeId.TryGetValue(challenge.Id, out var progress) &&
            (progress.IsComplete || progress.CurrentCount >= challenge.TotalThreshold))
        {
            return ChallengeStateEF.Completed;
        }

        if (challenge.PrerequisiteIds is null or [])
        {
            return ChallengeStateEF.Active;
        }

        if (challenge.PrerequisiteLogicalCondition is LogicalConditionEF.And)
        {
            foreach (var prereqId in challenge.PrerequisiteIds)
            {
                if (!progressByChallengeId.TryGetValue(prereqId, out var prereqProgress) ||
                    !(prereqProgress.IsComplete || (allChallenges.TryGetValue(prereqId, out var prereqDef) && prereqProgress.CurrentCount >= prereqDef.TotalThreshold)))
                {
                    return ChallengeStateEF.Locked;
                }
            }

            return ChallengeStateEF.Active;
        }
        else // Or
        {
            foreach (var prereqId in challenge.PrerequisiteIds)
            {
                if (progressByChallengeId.TryGetValue(prereqId, out var prereqProgress) &&
                    (prereqProgress.IsComplete || (allChallenges.TryGetValue(prereqId, out var prereqDef) && prereqProgress.CurrentCount >= prereqDef.TotalThreshold)))
                {
                    return ChallengeStateEF.Active;
                }
            }

            return ChallengeStateEF.Locked;
        }
    }

    private static Guid GetSeasonSlotGuid(Guid seasonId, byte slotIndex)
    {
        Span<byte> bytes = stackalloc byte[16];
        seasonId.TryWriteBytes(bytes);
        bytes[15] ^= (byte)(0x80 | slotIndex);
        
        return new Guid(bytes);
    }

    public void Dispose()
        => _seasonLock.Dispose();

    [LoggerMessage(EventId = 1, Level = LogLevel.Error, Message = "Error in seasons event bus subscriber")]
    private static partial void LogSubscriberError(ILogger logger, Exception exception);

    [LoggerMessage(EventId = 2, Level = LogLevel.Information, Message = "Received seasons_data_updated event. Invalidating season cache.")]
    private static partial void LogSeasonsDataUpdated(ILogger logger);
}
