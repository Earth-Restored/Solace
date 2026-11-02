using Solace.Db.Earth.Models.Common;

namespace Solace.Db.Earth.Models.Challenges;

public sealed class ChallengeDefinitionEF
{
    public Guid Id { get; set; }

    // Nullable for non-season challenges (Daily, Career, Tappable)
    public Guid? SeasonId { get; set; }
    public SeasonEF? Season { get; set; }

    public Guid? ParentId { get; set; }

    public List<Guid> PrerequisiteIds { get; set; } = [];

    public LogicalConditionEF PrerequisiteLogicalCondition { get; set; } = LogicalConditionEF.And;

    // position - 0,1,2 - first row; 0 is reserved
    public int Order { get; set; }

    public int TotalThreshold { get; set; }

    public ObjectiveTypeEF ObjectiveType { get; set; }

    public Guid? ObjectiveTarget { get; set; }

    public bool IsFinalSeasonChallenge { get; set; }

    public Rewards Rewards { get; set; } = new();
}
