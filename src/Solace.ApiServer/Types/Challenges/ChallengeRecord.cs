using Solace.ApiServer.Types.Common;

namespace Solace.ApiServer.Types.Challenges;

internal sealed record ChallengeRecord(
    string ReferenceId,
    string? ParentId,
    string GroupId,
    ChallengeDuration Duration,
    ChallengeType Type,
    string Category,
    Rarity? Rarity,
    int Order,
    string EndTimeUtc,
    ChallengeState State,
    bool IsComplete,
    int PercentComplete,
    int CurrentCount,
    int TotalThreshold,
    string[] PrerequisiteIds,
    LogicalCondition PrerequisiteLogicalCondition,
    Rewards Rewards,
    Dictionary<string, string> ClientProperties
);
