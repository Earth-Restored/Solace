namespace Solace.WebPortal.Common.Features.Seasons;

public sealed record PlayerActiveSeasonDto(
    SeasonPreviewDto? Season,
    PlayerSeasonProgressDto? Progress
);
