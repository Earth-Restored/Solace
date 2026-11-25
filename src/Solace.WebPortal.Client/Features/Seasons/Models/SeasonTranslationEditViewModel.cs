using Solace.WebPortal.Common.Features.Seasons;

namespace Solace.WebPortal.Client.Features.Seasons.Models;

public sealed class SeasonTranslationEditViewModel
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string IntroBody { get; set; } = string.Empty;
    public string EventProgress { get; set; } = string.Empty;
    public string EventComplete { get; set; } = string.Empty;

    public static SeasonTranslationEditViewModel FromDto(SeasonTranslationDto dto)
        => new()
        {
            Name = dto.Name ?? string.Empty,
            Description = dto.Description ?? string.Empty,
            IntroBody = dto.IntroBody ?? string.Empty,
            EventProgress = dto.EventProgress ?? string.Empty,
            EventComplete = dto.EventComplete ?? string.Empty,
        };

    public SeasonTranslationDto ToDto()
        => new(
            NullIfWhiteSpace(Name),
            NullIfWhiteSpace(Description),
            NullIfWhiteSpace(IntroBody),
            NullIfWhiteSpace(EventProgress),
            NullIfWhiteSpace(EventComplete)
        );

    private static string? NullIfWhiteSpace(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
