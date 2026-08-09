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
    private readonly StaticData.StaticData _staticData;

    public ChallengeActionsController(EarthDbContext earthDb, StaticData.StaticData staticData)
    {
        _earthDb = earthDb;
        _staticData = staticData;
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
        TokensEF.ChallengeProgressToken stored = tokens.Tokens.TryGetValue(ChallengesController.ProgressTokenId, out TokensEF.Token? raw) &&
            raw is TokensEF.ChallengeProgressToken progressToken
            ? progressToken
            : new TokensEF.ChallengeProgressToken();
        var progress = ChallengeProgressVersion.FromToken(stored);
        progress.EnsureDate(now);
        if (ChallengesController.TryGetDailyLoginDay(challengeId, out int requestedDay))
        {
            TokensEF.TokenWithId? token = ChallengesController.EnsureDailyLoginToken(tokens, progress, now);
            if (requestedDay != progress.GetDailyLoginDay(now) ||
                progress.IsDailyLoginClaimed(now) ||
                token?.Token is not TokensEF.DailyLoginToken dailyLoginToken)
            {
                return TypedResults.BadRequest();
            }

            tokens.AddToken(ChallengesController.ProgressTokenId, progress.ToToken());
            var results = new EarthDbContext.Results(_earthDb);
            TokensEF.Token? redeemed = await TokenUtils.RedeemTokenAsync(
                results, accountId, token.Id, now, _staticData, cancellationToken);
            if (redeemed is null)
            {
                return TypedResults.BadRequest();
            }

            var dailyUpdates = new EarthApiResponse.UpdatesResponse(results);
            dailyUpdates.Map["challenges"] = (int)(now / 1000);
            return EarthJson(new Dictionary<string, object?>
            {
                ["challengeId"] = challengeId,
                ["state"] = "Claimed",
                ["rewards"] = RedeemRewards.FromDBRewardsModel(dailyLoginToken.Rewards).ToApiResponse(),
                ["updates"] = new Dictionary<string, object>(),
            }, dailyUpdates);
        }

        progress.ClaimedChallengeIds.Add(challengeId);
        progress.ActiveSeasonId = ChallengesController.ActiveSeasonId;
        progress.ActiveSeasonChallengeId = ChallengesController.SelectActiveSeasonChallengeId(progress, progress.ActiveSeasonChallengeId);
        progress.UpdatedAt = now;
        tokens.AddToken(ChallengesController.ProgressTokenId, progress.ToToken());
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
