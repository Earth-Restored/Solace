namespace Solace.WebPortal.Common.Features.Seasons;

public sealed record PlayerChallengeProgressDto(
    int CurrentCount,
    bool IsComplete
);
