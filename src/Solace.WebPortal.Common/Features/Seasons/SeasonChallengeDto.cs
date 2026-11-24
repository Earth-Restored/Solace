namespace Solace.WebPortal.Common.Features.Seasons;

public sealed record SeasonChallengeDto(
    Guid Id,
    int Order,
    int TotalThreshold,
    IReadOnlyList<Guid> PrerequisiteIds,
    bool PrerequisiteIsOr,
    bool IsFinalSeasonChallenge,
    bool IsSeasonCapstone,
    IReadOnlyDictionary<string, ChallengeTranslationDto> Translations,
    SeasonRewardsDto Rewards,
    string? FrameTexture = null,
    string? TopDecorationTexture = null,
    string? BottomDecorationTexture = null,
    string? GlowColor = null,
    bool RewardSparkles = false
);
