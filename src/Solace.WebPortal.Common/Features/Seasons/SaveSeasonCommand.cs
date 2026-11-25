namespace Solace.WebPortal.Common.Features.Seasons;

public sealed record SaveSeasonCommand(
    int SeasonNumber,
    IReadOnlyDictionary<string, SeasonTranslationDto> Translations,
    SeasonRewardsDto? ExclusiveRewards,
    IReadOnlyList<SeasonChallengeDto> Challenges,
    IReadOnlyDictionary<string, byte[]>? TextureOverrides = null
);
