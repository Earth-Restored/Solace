using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http.HttpResults;
using Solace.ApiServer.Utils;

namespace Solace.ApiServer.Controllers;

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
        var now = HttpContext.GetTimestamp();
        var endDate = now.UtcDateTime.Date.AddDays(30);
        var endsAt = new DateTimeOffset(endDate, TimeSpan.Zero).ToUnixTimeMilliseconds();

        return EarthJson(new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["activeSeasonId"] = ActiveSeasonId,
            ["seasonId"] = ActiveSeasonId,
            ["title"] = "Season 17",
            ["startTimeUtc"] = TimeFormatter.FormatTime(now - TimeSpan.FromDays(1)),
            ["endTimeUtc"] = TimeFormatter.FormatTime(endsAt),
            ["premiumPassOwned"] = true,
            ["currentTier"] = 1,
            ["currentXp"] = 0,
            ["tiers"] = new[]
            {
                new Dictionary<string, object>(StringComparer.Ordinal)
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
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Endpoints cannot be static")]
    public ContentHttpResult PurchaseSeasonPass()
        => EarthJson(new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["premiumPassOwned"] = true
        });

    [HttpPost("challenges/season/active/{id}")]
    [HttpPut("challenges/season/active/{id}")]
    [HttpPost("player/challenges/season/active/{id}")]
    [HttpPut("player/challenges/season/active/{id}")]
    public Results<ContentHttpResult, BadRequest> SetActiveSeasonChallenge(string id)
    {
        var selectedChallengeId = string.IsNullOrWhiteSpace(id) ? DefaultActiveSeasonChallengeId : id;
        var now = HttpContext.GetTimestamp();
        var updates = new EarthApiResponse.UpdatesResponse();
        updates.Map["challenges"] = (int)now.ToUnixTimeSeconds();

        return EarthJson(new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["activeSeasonChallenge"] = selectedChallengeId,
            ["activeChallengeId"] = selectedChallengeId,
            ["activeSeasonId"] = ActiveSeasonId,
            ["seasonId"] = ActiveSeasonId,
        }, updates);
    }
}
