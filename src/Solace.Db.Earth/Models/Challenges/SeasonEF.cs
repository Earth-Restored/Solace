using Solace.Db.Earth.Models.Common;

namespace Solace.Db.Earth.Models.Challenges;

public sealed class SeasonEF
{
    public Guid Id { get; set; }

    public int SeasonNumber { get; set; }

    public required string Key { get; set; } // e.g. "season_1"

    public DateTimeOffset StartDate { get; set; }

    public DateTimeOffset EndDate { get; set; }

    public bool IsActive { get; set; }

    public int Version { get; set; } = 1;

    public Rewards? ExclusiveRewards { get; set; }

    public List<ChallengeDefinitionEF> Challenges { get; set; } = [];
}
