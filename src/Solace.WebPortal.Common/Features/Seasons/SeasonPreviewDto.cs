namespace Solace.WebPortal.Common.Features.Seasons;

public sealed record SeasonPreviewDto(
    Guid Id,
    int SeasonNumber,
    DateTimeOffset? StartDate,
    DateTimeOffset? EndDate,
    bool IsActive,
    string? LockedPathColor,
    string? UnlockedPathColor,
    IReadOnlyDictionary<string, SeasonTranslationDto> Translations,
    SeasonRewardsDto? ExclusiveRewards,
    IReadOnlyList<SeasonChallengeDto> Challenges
);
