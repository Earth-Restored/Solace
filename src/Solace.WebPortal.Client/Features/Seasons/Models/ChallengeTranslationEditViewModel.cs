using Solace.WebPortal.Common.Features.Seasons;

namespace Solace.WebPortal.Client.Features.Seasons.Models;

public sealed class ChallengeTranslationEditViewModel
{
    public string Description { get; set; } = string.Empty;
    public string DescriptionMultiple { get; set; } = string.Empty;

    public static ChallengeTranslationEditViewModel FromDto(ChallengeTranslationDto dto)
        => new()
        {
            Description = dto.Description ?? string.Empty,
            DescriptionMultiple = dto.DescriptionMultiple ?? string.Empty,
        };

    public ChallengeTranslationDto ToDto()
        => new(
            string.IsNullOrWhiteSpace(Description) ? null : Description.Trim(),
            string.IsNullOrWhiteSpace(DescriptionMultiple) ? null : DescriptionMultiple.Trim()
        );
}
