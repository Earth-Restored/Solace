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
    public IActionResult ModifyState(string challengeId)
    {
        long now = HttpContext.GetTimestamp();
        var updates = new EarthApiResponse.UpdatesResponse();
        updates.Map["challenges"] = (int)(now / 1000);

        return Content(Json.Serialize(new EarthApiResponse(new Dictionary<string, object?>
        {
            ["challengeId"] = challengeId,
            ["state"] = "Claimed",
            ["rewards"] = new RedeemRewards().ToApiResponse(),
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
    public IActionResult RemoveContinuousChallenge(string id)
    {
        long now = HttpContext.GetTimestamp();
        var updates = new EarthApiResponse.UpdatesResponse();
        updates.Map["challenges"] = (int)(now / 1000);

        return Content(Json.Serialize(new EarthApiResponse(new Dictionary<string, object?>
        {
            ["challengeId"] = id,
            ["updates"] = new Dictionary<string, object>()
        }, updates)), "application/json");
    }
}
