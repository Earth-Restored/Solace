using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http.HttpResults;
using Solace.ApiServer.Utils;
using Solace.Common.Utils;

namespace Solace.ApiServer.Controllers.EarthApi;

[Authorize]
[ApiVersion("1.1")]
[Route("1/api/v{version:apiVersion}")]
internal sealed class SeasonsController : SolaceControllerBase
{
    private const string ActiveSeasonId = "00000000-0000-0000-0000-000000000001";
    private const string DefaultActiveSeasonChallengeId = "00000000-0000-0000-0000-000000000000";

    [HttpGet("player/season")]
    [HttpGet("player/seasons")]
    [HttpGet("player/seasonpass")]
    [HttpGet("season")]
    [HttpGet("seasons")]
    public ContentHttpResult GetSeason()
    {
        long now = HttpContext.GetTimestamp();
        DateTime endDate = DateTimeOffset.FromUnixTimeMilliseconds(now).UtcDateTime.Date.AddDays(30);
        long endsAt = new DateTimeOffset(endDate, TimeSpan.Zero).ToUnixTimeMilliseconds();

        return EarthJson(new Dictionary<string, object>
        {
            ["activeSeasonId"] = ActiveSeasonId,
            ["seasonId"] = ActiveSeasonId,
            ["title"] = "Season 17",
            ["startTimeUtc"] = TimeFormatter.FormatTime(now - 24 * 60 * 60 * 1000),
            ["endTimeUtc"] = TimeFormatter.FormatTime(endsAt),
            ["premiumPassOwned"] = true,
            ["currentTier"] = 1,
            ["currentXp"] = 0,
            ["tiers"] = new[]
            {
                new Dictionary<string, object>
                {
                    ["tier"] = 1,
                    ["xpRequired"] = 0,
                    ["freeRewards"] = Array.Empty<object>(),
                    ["premiumRewards"] = Array.Empty<object>()
                }
            }
        });
    }

    [HttpPost("player/seasonpass/purchase")]
    [HttpPost("seasonpass/purchase")]
    public ContentHttpResult PurchaseSeasonPass()
        => EarthJson(new Dictionary<string, object>
        {
            ["premiumPassOwned"] = true
        });

    [HttpPost("challenges/season/active/{id}")]
    [HttpPut("challenges/season/active/{id}")]
    [HttpPost("player/challenges/season/active/{id}")]
    [HttpPut("player/challenges/season/active/{id}")]
    public Results<ContentHttpResult, BadRequest> SetActiveSeasonChallenge(string id)
    {
        string selectedChallengeId = string.IsNullOrWhiteSpace(id) ? DefaultActiveSeasonChallengeId : id;
        long now = HttpContext.GetTimestamp();
        var updates = new EarthApiResponse.UpdatesResponse();
        updates.Map["challenges"] = (int)(now / 1000);

        return EarthJson(new Dictionary<string, object>
        {
            ["activeSeasonChallenge"] = selectedChallengeId,
            ["activeChallengeId"] = selectedChallengeId,
            ["activeSeasonId"] = ActiveSeasonId,
            ["seasonId"] = ActiveSeasonId,
        }, updates);
    }
}
