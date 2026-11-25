using Solace.WebPortal.Common.Features.Seasons;

namespace Solace.WebPortal.Client.Features.Seasons.Models;

public sealed class SeasonRewardsEditViewModel
{
    public int Rubies { get; set; }
    public int ExperiencePoints { get; set; }
    public int? Level { get; set; }
    public List<SeasonRewardItemEditViewModel> Items { get; set; } = [];
    public List<Guid> Buildplates { get; set; } = [];
    public List<Guid> PersonaItems { get; set; } = [];

    public bool HasAnyReward => Rubies > 0
        || ExperiencePoints > 0
        || Level is > 0
        || Items.Count > 0
        || Buildplates.Count > 0
        || PersonaItems.Count > 0;

    public static SeasonRewardsEditViewModel FromDto(SeasonRewardsDto? dto)
    {
        if (dto is null)
        {
            return new SeasonRewardsEditViewModel();
        }

        return new SeasonRewardsEditViewModel
        {
            Rubies = dto.Rubies,
            ExperiencePoints = dto.ExperiencePoints,
            Level = dto.Level,
            Items = [.. dto.Items.Select(i => new SeasonRewardItemEditViewModel { Id = i.Id, Amount = i.Amount })],
            Buildplates = [.. dto.Buildplates],
            PersonaItems = [.. dto.PersonaItems],
        };
    }

    public SeasonRewardsDto ToDto()
        => new(
            int.Max(0, Rubies),
            int.Max(0, ExperiencePoints),
            Level is > 0 ? Level : null,
            [.. Items.Where(item => item.Id != Guid.Empty && item.Amount > 0).Select(item => new SeasonRewardItemDto(item.Id, item.Amount))],
            [.. Buildplates.Where(id => id != Guid.Empty).Distinct()],
            [.. PersonaItems.Where(id => id != Guid.Empty).Distinct()]
        );
}
