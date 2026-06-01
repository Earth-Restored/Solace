using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Solace.ApiServer.Utils;
using Solace.Common.Utils;
using RedeemRewards = Solace.ApiServer.Utils.Rewards;

namespace Solace.ApiServer.Controllers.EarthApi;

[Authorize]
[ApiVersion("1.1")]
[Route("1/api/v{version:apiVersion}/challenges")]
internal sealed class ChallengeActionsController : SolaceControllerBase
{
    [HttpPost("{challengeId}/modifyState")]
    [HttpPut("{challengeId}/modifyState")]
    public ContentHttpResult ModifyState(string challengeId)
    {
        long now = HttpContext.GetTimestamp();
        var updates = new EarthApiResponse.UpdatesResponse();
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
