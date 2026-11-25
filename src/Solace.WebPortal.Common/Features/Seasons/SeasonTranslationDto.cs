namespace Solace.WebPortal.Common.Features.Seasons;

public sealed record SeasonTranslationDto(
    string? Name,
    string? Description,
    string? IntroBody = null,
    string? EventProgress = null,
    string? EventComplete = null
);
