using Solace.WebPortal.Common.Features.Seasons;

namespace Solace.WebPortal.Client.Features.Seasons.Models;

public sealed class SeasonEditViewModel
{
    public Guid Id { get; set; }
    public int SeasonNumber { get; set; } = 1;
    public DateTimeOffset? StartDate { get; set; }
    public DateTimeOffset? EndDate { get; set; }
    public bool IsActive { get; set; }
    public Dictionary<string, SeasonTranslationEditViewModel> Translations { get; set; } = new(StringComparer.Ordinal)
    {
        ["en_US"] = new(),
    };
    public SeasonRewardsEditViewModel ExclusiveRewards { get; set; } = new();
    public List<SeasonChallengeEditViewModel> Challenges { get; set; } = [];
    public Dictionary<string, byte[]> TextureOverrides { get; set; } = [with(StringComparer.Ordinal)];
    public Dictionary<string, string> TexturePreviewDataUrls { get; set; } = [with(StringComparer.Ordinal)];

    public static SeasonEditViewModel FromDto(SeasonPreviewDto dto)
    {
        var translations = dto.Translations.ToDictionary(
            translation => translation.Key,
            translation => SeasonTranslationEditViewModel.FromDto(translation.Value),
            StringComparer.Ordinal);

        if (!translations.ContainsKey("en_US"))
        {
            translations["en_US"] = new SeasonTranslationEditViewModel();
        }

        return new SeasonEditViewModel
        {
            Id = dto.Id,
            SeasonNumber = dto.SeasonNumber,
            StartDate = dto.StartDate,
            EndDate = dto.EndDate,
            IsActive = dto.IsActive,
            Translations = translations,
            ExclusiveRewards = SeasonRewardsEditViewModel.FromDto(dto.ExclusiveRewards),
            Challenges = [.. dto.Challenges.OrderBy(c => c.Order).Select(SeasonChallengeEditViewModel.FromDto)],
        };
    }

    public SeasonTranslationEditViewModel EnsureDefaultTranslation()
    {
        if (!Translations.TryGetValue("en_US", out var english))
        {
            english = new SeasonTranslationEditViewModel();
            Translations["en_US"] = english;
        }

        return english;
    }

    public SeasonPreviewDto ToPreviewDto()
    {
        var ordered = Challenges.OrderBy(challenge => challenge.Order).ToList();
        var sparkleIds = ComputeSparkleIds(ordered);

        return new SeasonPreviewDto(
            Id,
            SeasonNumber,
            StartDate,
            EndDate,
            IsActive,
            LockedPathColor: null,
            UnlockedPathColor: null,
            Translations.ToDictionary(translation => translation.Key, translation => translation.Value.ToDto(), StringComparer.Ordinal),
            ExclusiveRewards.HasAnyReward ? ExclusiveRewards.ToDto() : null,
            [.. ordered.Select(challenge => challenge.ToDto(sparkleIds.Contains(challenge.Id)))]
        );
    }

    public SaveSeasonCommand ToSaveCommand()
        => new(
            SeasonNumber,
            Translations.ToDictionary(translation => translation.Key, translation => translation.Value.ToDto(), StringComparer.Ordinal),
            ExclusiveRewards.HasAnyReward ? ExclusiveRewards.ToDto() : null,
            [.. Challenges.OrderBy(challenge => challenge.Order).Select(c => c.ToDto())],
            TextureOverrides.Count > 0 ? TextureOverrides : null
        );

    private static HashSet<Guid> ComputeSparkleIds(List<SeasonChallengeEditViewModel> ordered)
    {
        var capstone = ordered.LastOrDefault(challenge => challenge.IsFinalSeasonChallenge || challenge.ObjectiveType is ChallengeObjectiveTypeDto.SeasonCapstone)
            ?? ordered.LastOrDefault();

        var milestone = ordered.FirstOrDefault(challenge => challenge != capstone && challenge.Rewards.PersonaItems.Count > 0)
            ?? (ordered.Count >= 2 ? ordered[(ordered.Count - 2) / 2] : null);

        var ids = new HashSet<Guid>();
        if (milestone is not null)
        {
            ids.Add(milestone.Id);
        }

        if (capstone is not null)
        {
            ids.Add(capstone.Id);
        }

        return ids;
    }
}
