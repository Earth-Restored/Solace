using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http.HttpResults;
using System.Security.Claims;
using Solace.ApiServer.Utils;
using Solace.Common;
using Solace.Common.Utils;
using Solace.DB;

namespace Solace.ApiServer.Controllers.EarthApi;

[Authorize]
[ApiVersion("1.1")]
[Route("1/api/v{version:apiVersion}")]
internal sealed class SeasonsController : ControllerBase
{
    [HttpGet("player/season")]
    [HttpGet("player/seasons")]
    [HttpGet("player/seasonpass")]
    [HttpGet("season")]
    [HttpGet("seasons")]
    public IActionResult GetSeason()
    {
        long now = HttpContext.GetTimestamp();
        long endsAt = new DateTimeOffset(DateTimeOffset.FromUnixTimeMilliseconds(now).UtcDateTime.Date.AddDays(30)).ToUnixTimeMilliseconds();

        return Content(Json.Serialize(new EarthApiResponse(new Dictionary<string, object>
        {
            ["activeSeasonId"] = ChallengesController.ActiveSeasonId,
            ["seasonId"] = ChallengesController.ActiveSeasonId,
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
        })), "application/json");
    }

    [HttpPost("player/seasonpass/purchase")]
    [HttpPost("seasonpass/purchase")]
    public IActionResult PurchaseSeasonPass()
        => Content(Json.Serialize(new EarthApiResponse(new Dictionary<string, object>
        {
            ["premiumPassOwned"] = true
        })), "application/json");

    [HttpPost("challenges/season/active/{id}")]
    [HttpPut("challenges/season/active/{id}")]
    [HttpPost("player/challenges/season/active/{id}")]
    [HttpPut("player/challenges/season/active/{id}")]
    public async Task<Results<ContentHttpResult, BadRequest>> SetActiveSeasonChallenge(string id, CancellationToken cancellationToken)
    {
        string? playerId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(playerId))
        {
            return TypedResults.BadRequest();
        }

        string activeChallengeId = string.IsNullOrWhiteSpace(id) ? ChallengesController.DefaultActiveSeasonChallengeId : id;
        long now = HttpContext.GetTimestamp();

        EarthDB.Results results = await new EarthDB.Query(true)
            .Get("challenges", playerId, typeof(ChallengeProgressVersion))
            .Then(results =>
            {
                ChallengeProgressVersion progress = results.Get<ChallengeProgressVersion>("challenges");
                progress.ActiveSeasonId = ChallengesController.ActiveSeasonId;
                progress.ActiveSeasonChallengeId = ChallengesController.SelectActiveSeasonChallengeId(progress, activeChallengeId);
                progress.UpdatedAt = now;

                return new EarthDB.Query(true)
                    .Update("challenges", playerId, progress)
                    .Extra("activeSeasonChallenge", progress.ActiveSeasonChallengeId);
            })
            .ExecuteAsync(Program.DB, cancellationToken);

        var updates = new EarthApiResponse.UpdatesResponse(results);
        updates.Map["challenges"] = (int)(now / 1000);
        string selectedChallengeId = (string)results.GetExtra("activeSeasonChallenge");

        return TypedResults.Content(Json.Serialize(new EarthApiResponse(new Dictionary<string, object>
        {
            ["activeSeasonChallenge"] = selectedChallengeId,
            ["activeChallengeId"] = selectedChallengeId,
            ["activeSeasonId"] = ChallengesController.ActiveSeasonId,
            ["seasonId"] = ChallengesController.ActiveSeasonId,
        }, updates)), "application/json");
    }
}
