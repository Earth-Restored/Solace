using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Solace.ApiServer.Utils;
using Solace.Common.Utils;
using Solace.DB;
using Solace.DB.Models.Player;
using Solace.DB.Utils;
using RedeemRewards = Solace.ApiServer.Utils.Rewards;

namespace Solace.ApiServer.Controllers.EarthApi;

[Authorize]
[ApiVersion("1.1")]
[Route("1/api/v{version:apiVersion}/challenges")]
internal sealed class ChallengeActionsController : SolaceControllerBase
{
    private readonly EarthDbContext _earthDb;

    public ChallengeActionsController(EarthDbContext earthDb)
    {
        _earthDb = earthDb;
    }

    [HttpPost("{challengeId}/modifyState")]
    [HttpPut("{challengeId}/modifyState")]
    public async Task<Results<ContentHttpResult, BadRequest>> ModifyState(string challengeId, CancellationToken cancellationToken)
    {
        if (!TryGetAccountId(out Guid accountId))
        {
            return TypedResults.BadRequest();
        }

        long now = HttpContext.GetTimestamp();
        var tokens = await _earthDb.Tokens
            .AsTracking()
            .FirstOrNewAsync(tokens => tokens.Id == accountId, cancellationToken: cancellationToken);
        TokensEF.ChallengeProgressToken stored = tokens.Tokens.TryGetValue("__challenge_progress", out TokensEF.Token? raw) &&
            raw is TokensEF.ChallengeProgressToken progressToken
            ? progressToken
            : new TokensEF.ChallengeProgressToken();
        var progress = new ChallengeProgressVersion
        {
            UpdatedAt = stored.UpdatedAt,
            DailyDateUtc = stored.DailyDateUtc,
            ActiveSeasonId = stored.ActiveSeasonId,
            ActiveSeasonChallengeId = stored.ActiveSeasonChallengeId,
            TappablesRedeemed = stored.TappablesRedeemed,
            ObjectiveCounts = new(stored.ObjectiveCounts),
            ClaimedChallengeIds = [.. stored.ClaimedChallengeIds],
            RemovedContinuousChallengeIds = [.. stored.RemovedContinuousChallengeIds]
        };
        progress.EnsureDate(now);
        progress.ClaimedChallengeIds.Add(challengeId);
        progress.ActiveSeasonId = ChallengesController.ActiveSeasonId;
        progress.ActiveSeasonChallengeId = ChallengesController.SelectActiveSeasonChallengeId(progress, progress.ActiveSeasonChallengeId);
        progress.UpdatedAt = now;
        tokens.AddToken("__challenge_progress", new TokensEF.ChallengeProgressToken
        {
            UpdatedAt = progress.UpdatedAt,
            DailyDateUtc = progress.DailyDateUtc,
            ActiveSeasonId = progress.ActiveSeasonId,
            ActiveSeasonChallengeId = progress.ActiveSeasonChallengeId,
            TappablesRedeemed = progress.TappablesRedeemed,
            ObjectiveCounts = new(progress.ObjectiveCounts),
            ClaimedChallengeIds = [.. progress.ClaimedChallengeIds],
            RemovedContinuousChallengeIds = [.. progress.RemovedContinuousChallengeIds]
        });
        await _earthDb.SaveChangesAsync(cancellationToken);

        var updates = new EarthApiResponse.UpdatesResponse(new EarthDbContext.Results(_earthDb));
        updates.Map["challenges"] = (int)(now / 1000);
        return EarthJson(new Dictionary<string, object?>
        {
            ["challengeId"] = challengeId,
            ["state"] = "Claimed",
            ["rewards"] = new RedeemRewards().ToApiResponse(),
            ["updates"] = new Dictionary<string, object>()
        }, updates);
    }

    [HttpPost("timed/generate")]
    [HttpPut("timed/generate")]
    public ContentHttpResult GenerateTimedChallenges()
        => EarthJson(new Dictionary<string, object?>
        {
            ["updates"] = new Dictionary<string, object>()
        });

    [HttpPost("reset")]
    [HttpPut("reset")]
    public ContentHttpResult ResetChallenges()
        => EarthJson(new Dictionary<string, object?>
        {
            ["updates"] = new Dictionary<string, object>()
        });

    [HttpPost("continuous/{id}/remove")]
    [HttpDelete("continuous/{id}/remove")]
    public ContentHttpResult RemoveContinuousChallenge(string id)
    {
        long now = HttpContext.GetTimestamp();
        var updates = new EarthApiResponse.UpdatesResponse();
        updates.Map["challenges"] = (int)(now / 1000);

        return EarthJson(new Dictionary<string, object?>
        {
            ["challengeId"] = id,
            ["updates"] = new Dictionary<string, object>()
        }, updates);
    }
}
