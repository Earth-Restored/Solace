namespace Solace.ApiServer.Types.Challenges;

internal sealed record ChallengesResponse(
    Dictionary<Guid, ChallengeRecord> Challenges,
    Guid ActiveSeasonChallenge
);
