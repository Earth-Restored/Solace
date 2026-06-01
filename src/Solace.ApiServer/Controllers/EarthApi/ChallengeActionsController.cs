using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Solace.DB;
using Solace.ApiServer.Utils;
using Solace.Common;
using Solace.Common.Utils;
using System.Security.Claims;
using ApiRewards = Solace.ApiServer.Types.Common.Rewards;
using RedeemRewards = Solace.ApiServer.Utils.Rewards;

namespace Solace.ApiServer.Controllers.EarthApi;

[Authorize]
[ApiVersion("1.1")]
[Route("1/api/v{version:apiVersion}/challenges")]
internal sealed class ChallengeActionsController : ControllerBase
{
    [HttpPost("{challengeId}/modifyState")]
    [HttpPut("{challengeId}/modifyState")]
    public async Task<IActionResult> ModifyState(string challengeId, CancellationToken cancellationToken)
    {
        string? playerId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(playerId))
        {
            return BadRequest();
        }

        long now = HttpContext.GetTimestamp();
        ApiRewards? apiRewards = ChallengesController.GetSeasonChallengeRewards(challengeId);
        RedeemRewards rewards = ToRedeemRewards(apiRewards);

        EarthDB.Results results = await new EarthDB.Query(true)
            .Get("challenges", playerId, typeof(ChallengeProgressVersion))
            .Then(queryResults =>
            {
                ChallengeProgressVersion progress = queryResults.Get<ChallengeProgressVersion>("challenges");
                progress.EnsureDate(now);
                progress.ClaimedChallengeIds ??= [];

                bool firstClaim = progress.ClaimedChallengeIds.Add(challengeId);
                progress.ActiveSeasonId = ChallengesController.ActiveSeasonId;
                progress.ActiveSeasonChallengeId = ChallengesController.SelectActiveSeasonChallengeId(progress, progress.ActiveSeasonChallengeId);
                progress.UpdatedAt = now;

                var query = new EarthDB.Query(true)
                    .Update("challenges", playerId, progress);

                if (firstClaim && apiRewards is not null)
                {
                    query.Then(rewards.ToRedeemQuery(playerId, now, Program.staticData), false);
                }

                return query;
            })
            .ExecuteAsync(Program.DB, cancellationToken);

        var updates = new EarthApiResponse.UpdatesResponse(results);
        updates.Map["challenges"] = (int)(now / 1000);

        return Content(Json.Serialize(new EarthApiResponse(new Dictionary<string, object?>
        {
            ["challengeId"] = challengeId,
            ["state"] = "Claimed",
            ["rewards"] = apiRewards ?? rewards.ToApiResponse(),
            ["updates"] = new Dictionary<string, object>()
        }, updates)), "application/json");
    }

    [HttpPost("timed/generate")]
    [HttpPut("timed/generate")]
    public IActionResult GenerateTimedChallenges()
        => Content(Json.Serialize(new EarthApiResponse(new Dictionary<string, object?>
        {
            ["updates"] = new Dictionary<string, object>()
        })), "application/json");

    [HttpPost("reset")]
    [HttpPut("reset")]
    public IActionResult ResetChallenges()
        => Content(Json.Serialize(new EarthApiResponse(new Dictionary<string, object?>
        {
            ["updates"] = new Dictionary<string, object>()
        })), "application/json");

    [HttpPost("continuous/{id}/remove")]
    [HttpDelete("continuous/{id}/remove")]
    public async Task<IActionResult> RemoveContinuousChallenge(string id, CancellationToken cancellationToken)
    {
        string? playerId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(playerId))
        {
            return BadRequest();
        }

        long now = HttpContext.GetTimestamp();
        EarthDB.Results results = await new EarthDB.Query(true)
            .Get("challenges", playerId, typeof(ChallengeProgressVersion))
            .Then(queryResults =>
            {
                ChallengeProgressVersion progress = queryResults.Get<ChallengeProgressVersion>("challenges");
                progress.EnsureDate(now);
                progress.RemovedContinuousChallengeIds ??= [];
                progress.RemovedContinuousChallengeIds.Add(id);
                progress.UpdatedAt = now;

                return new EarthDB.Query(true)
                    .Update("challenges", playerId, progress);
            })
            .ExecuteAsync(Program.DB, cancellationToken);

        var updates = new EarthApiResponse.UpdatesResponse(results);
        updates.Map["challenges"] = (int)(now / 1000);

        return Content(Json.Serialize(new EarthApiResponse(new Dictionary<string, object?>
        {
            ["challengeId"] = id,
            ["updates"] = new Dictionary<string, object>()
        }, updates)), "application/json");
    }

    private static RedeemRewards ToRedeemRewards(ApiRewards? rewards)
    {
        var result = new RedeemRewards();
        if (rewards is null)
        {
            return result;
        }

        if (rewards.Rubies is > 0)
        {
            result.AddRubies(rewards.Rubies.Value);
        }

        if (rewards.ExperiencePoints is > 0)
        {
            result.AddExperiencePoints(rewards.ExperiencePoints.Value);
        }

        foreach (var item in rewards.Inventory)
        {
            result.AddItem(item.Id, item.Amount);
        }

        foreach (string buildplateId in rewards.Buildplates)
        {
            result.AddBuildplate(buildplateId);
        }

        foreach (var challenge in rewards.Challenges)
        {
            result.AddChallenge(challenge.Id);
        }

        return result;
    }
}
