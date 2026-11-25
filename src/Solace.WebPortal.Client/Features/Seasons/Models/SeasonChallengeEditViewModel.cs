using System.Globalization;
using Solace.WebPortal.Common.Features.Seasons;

namespace Solace.WebPortal.Client.Features.Seasons.Models;

public sealed class SeasonChallengeEditViewModel
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public int Order { get; set; } = 1;
    public int TotalThreshold { get; set; } = 1;
    public List<Guid> PrerequisiteIds { get; set; } = [];
    public bool PrerequisiteIsOr { get; set; }
    public bool IsFinalSeasonChallenge { get; set; }
    public ChallengeObjectiveTypeDto ObjectiveType { get; set; } = ChallengeObjectiveTypeDto.TappableCollected;
    public Guid? ObjectiveTarget { get; set; }
    public Dictionary<string, ChallengeTranslationEditViewModel> Translations { get; set; } = new(StringComparer.Ordinal)
    {
        ["en_US"] = new(),
    };
    public SeasonRewardsEditViewModel Rewards { get; set; } = new();

    public static SeasonChallengeEditViewModel FromDto(SeasonChallengeDto dto)
    {
        var translations = dto.Translations.ToDictionary(
            translation => translation.Key,
            translation => ChallengeTranslationEditViewModel.FromDto(translation.Value),
            StringComparer.Ordinal);

        if (!translations.ContainsKey("en_US"))
        {
            translations["en_US"] = new ChallengeTranslationEditViewModel();
        }

        return new SeasonChallengeEditViewModel
        {
            Id = dto.Id,
            Order = dto.Order,
            TotalThreshold = dto.TotalThreshold,
            PrerequisiteIds = [.. dto.PrerequisiteIds],
            PrerequisiteIsOr = dto.PrerequisiteIsOr,
            IsFinalSeasonChallenge = dto.IsFinalSeasonChallenge || dto.IsSeasonCapstone,
            ObjectiveType = dto.ObjectiveType,
            ObjectiveTarget = dto.ObjectiveTarget,
            Translations = translations,
            Rewards = SeasonRewardsEditViewModel.FromDto(dto.Rewards),
        };
    }

    public SeasonChallengeDto ToDto(bool rewardSparkles = false)
        => new(
            Id,
            Order,
            TotalThreshold,
            [.. PrerequisiteIds],
            PrerequisiteIsOr,
            IsFinalSeasonChallenge || ObjectiveType is ChallengeObjectiveTypeDto.SeasonCapstone,
            ObjectiveType is ChallengeObjectiveTypeDto.SeasonCapstone,
            Translations.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.ToDto(), StringComparer.Ordinal),
            Rewards.ToDto(),
            RewardSparkles: rewardSparkles,
            ObjectiveType: ObjectiveType,
            ObjectiveTarget: ObjectiveTarget
        );

    public ChallengeTranslationEditViewModel EnsureDefaultTranslation()
    {
        if (!Translations.TryGetValue("en_US", out var en))
        {
            en = new ChallengeTranslationEditViewModel();
            Translations["en_US"] = en;
        }

        return en;
    }

    public string GetDisplayDescription()
    {
        if (Translations.TryGetValue("en_US", out var english))
        {
            if (TotalThreshold > 1 && !string.IsNullOrWhiteSpace(english.DescriptionMultiple))
            {
                return english.DescriptionMultiple.Replace("%d", TotalThreshold.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal);
            }

            if (!string.IsNullOrWhiteSpace(english.Description))
            {
                return english.Description;
            }
        }

        var first = Translations.Values.FirstOrDefault(t => !string.IsNullOrWhiteSpace(t.Description));
        return first?.Description ?? $"Challenge #{Order}";
    }
}
