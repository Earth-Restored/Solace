namespace Solace.Db.Earth.Models.Challenges;

public sealed class PlayerSeasonProgressEF
{
    public Guid ProfileId { get; set; }
    public ProfileEF Profile { get; set; } = null!;

    public Guid SeasonId { get; set; }
    public SeasonEF Season { get; set; } = null!;

    public Guid? ActiveSeasonChallengeId { get; set; }
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public bool IsCompleted { get; set; }
}
