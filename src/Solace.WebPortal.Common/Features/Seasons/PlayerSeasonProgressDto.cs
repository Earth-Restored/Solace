namespace Solace.WebPortal.Common.Features.Seasons;

public sealed record PlayerSeasonProgressDto(
    Guid? ActiveSeasonChallengeId,
    bool IsCompleted,
    DateTimeOffset? StartedAt,
    DateTimeOffset? CompletedAt,
    IReadOnlyDictionary<Guid, PlayerChallengeProgressDto> Challenges
);
