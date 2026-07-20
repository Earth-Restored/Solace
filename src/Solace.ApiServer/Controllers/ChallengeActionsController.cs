using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Solace.ApiServer.Utils;
using RedeemRewards = Solace.ApiServer.Utils.Rewards;

namespace Solace.ApiServer.Controllers;

[Authorize]
[ApiVersion("1.1")]
[Route("1/api/v{version:apiVersion}/challenges")]
internal sealed class ChallengeActionsController : SolaceControllerBase
{
    [HttpPost("{challengeId}/modifyState")]
    [HttpPut("{challengeId}/modifyState")]
    public ContentHttpResult ModifyState(string challengeId)
    {
        var now = HttpContext.GetTimestamp();
        var updates = new EarthApiResponse.UpdatesResponse();
        updates.Map["challenges"] = (int)now.ToUnixTimeSeconds();

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
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Endpoints cannot be static")]
    public ContentHttpResult GenerateTimedChallenges()
        => EarthJson(new Dictionary<string, object?>
        {
            ["updates"] = new Dictionary<string, object>()
        });

    [HttpPost("reset")]
    [HttpPut("reset")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Endpoints cannot be static")]
    public ContentHttpResult ResetChallenges()
        => EarthJson(new Dictionary<string, object?>
        {
            ["updates"] = new Dictionary<string, object>()
        });

    [HttpPost("continuous/{id}/remove")]
    [HttpDelete("continuous/{id}/remove")]
    public ContentHttpResult RemoveContinuousChallenge(string id)
    {
        var now = HttpContext.GetTimestamp();
        var updates = new EarthApiResponse.UpdatesResponse();
        updates.Map["challenges"] = (int)now.ToUnixTimeSeconds();

        return EarthJson(new Dictionary<string, object?>
        {
            ["challengeId"] = id,
            ["updates"] = new Dictionary<string, object>()
        }, updates);
    }
}
