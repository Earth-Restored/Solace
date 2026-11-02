namespace Solace.Db.Earth.Models.Challenges;

public sealed class PlayerChallengeProgressEF
{
    public Guid ProfileId { get; set; }
    public ProfileEF Profile { get; set; } = null!;

    public Guid ChallengeId { get; set; }
    public ChallengeDefinitionEF Challenge { get; set; } = null!;

    public int CurrentCount { get; set; }
    public ChallengeStateEF State { get; set; } = ChallengeStateEF.Locked;
    public bool IsComplete { get; set; }
    public DateTimeOffset? EndTimeUtc { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public DateTimeOffset? ClaimedAt { get; set; }
}
