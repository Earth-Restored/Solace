using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Solace.ApiServer.Utils;
using Solace.Common;
using Solace.Common.Utils;
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
        await Task.CompletedTask;
        long now = HttpContext.GetTimestamp();
        ApiRewards? apiRewards = null;
        RedeemRewards rewards = ToRedeemRewards(apiRewards);
        var updates = new EarthApiResponse.UpdatesResponse();
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
        await Task.CompletedTask;
        long now = HttpContext.GetTimestamp();
        var updates = new EarthApiResponse.UpdatesResponse();
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
