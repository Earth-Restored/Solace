using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Solace.ApiServer.Services;
using Solace.ApiServer.Types.Challenges;
using Solace.ApiServer.Utils;
using Solace.Db.Earth;

namespace Solace.ApiServer.Controllers;

[Authorize]
[ApiVersion("1.1")]
[Route("1/api/v{version:apiVersion}")]
internal sealed class ChallengesController : SolaceControllerBase
{
    private readonly EarthDbContext _earthDb;
    private readonly SeasonChallengeService _seasonChallengeService;

    public ChallengesController(EarthDbContext earthDb, SeasonChallengeService seasonChallengeService)
    {
        _earthDb = earthDb;
        _seasonChallengeService = seasonChallengeService;
    }

    [HttpGet("player/challenges")]
    public async Task<Results<Ok<EarthApiResponse<ChallengesResponse>>, BadRequest>> GetChallenges(CancellationToken cancellationToken)
    {
        if (!TryGetProfileId(out var profileId))
        {
            return TypedResults.BadRequest();
        }

        var response = await _seasonChallengeService.GetChallengesResponseAsync(_earthDb, profileId, cancellationToken);
        return TypedResults.Ok(new EarthApiResponse<ChallengesResponse>(response));
    }

    [HttpPut("challenges/season/active/{id}")]
    public async Task<Results<Ok<EarthApiResponse>, BadRequest>> SetActiveSeasonChallenge(Guid id, CancellationToken cancellationToken)
    {
        if (!TryGetProfileId(out var profileId))
        {
            return TypedResults.BadRequest();
        }

        var results = new ResultsEF.Builder();
        var success = await _seasonChallengeService.SetActiveSeasonChallengeAsync(_earthDb, profileId, id, results, cancellationToken);
        if (!success)
        {
            return TypedResults.BadRequest();
        }

        return TypedResults.Ok(new EarthApiResponse(new EarthUpdatesResponse(await results.BuildAsync(_earthDb, profileId, cancellationToken))));
    }
}
