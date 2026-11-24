namespace Solace.WebPortal.Common.Features.Seasons;

public sealed record SeasonRewardsDto(
    int Rubies,
    int ExperiencePoints,
    int? Level,
    IReadOnlyList<SeasonRewardItemDto> Items,
    IReadOnlyList<Guid> Buildplates,
    IReadOnlyList<Guid> PersonaItems
);
