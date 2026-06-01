using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using System.Globalization;
using System.Security.Claims;
using Solace.ApiServer.Utils;
using Solace.Common;
using Solace.DB;
using Solace.DB.Models.Player;
using DBRewards = Solace.DB.Models.Common.Rewards;

namespace Solace.ApiServer.Controllers.EarthApi;

[Authorize]
[ApiVersion("1.1")]
[Route("1/api/v{version:apiVersion}")]
internal sealed class DailyGoodiesController : SolaceControllerBase
{
    private const string CommonAdventureCrystalId = "4f16a053-4929-263a-c91a-29663e29df76";
    private static EarthDB earthDB => Program.DB;
    private static StaticData.StaticData staticData => Program.staticData;

    [HttpGet("player/dailygoodies")]
    [HttpGet("player/daily-goodies")]
    [HttpGet("player/daily-login")]
    [HttpGet("player/dailyrewards")]
    [HttpGet("dailygoodies")]
    [HttpGet("daily-goodies")]
    [HttpGet("daily-login")]
    [HttpGet("dailyrewards")]
    public async Task<Results<ContentHttpResult, BadRequest>> Get(CancellationToken cancellationToken)
    {
        if (!TryGetPlayerId(out string playerId))
        {
            return TypedResults.BadRequest();
        }

        await TokenUtils.EnsureDailyLoginToken(playerId, cancellationToken);
        string today = TodayUtc();

        EarthDB.Results results = await new EarthDB.Query(false)
            .Get("tokenClaims", playerId, typeof(TokenClaims))
            .Get("tokens", playerId, typeof(Tokens))
            .ExecuteAsync(earthDB, cancellationToken);

        TokenClaims tokenClaims = results.Get<TokenClaims>("tokenClaims");
        Tokens tokens = results.Get<Tokens>("tokens");

        bool claimed = tokenClaims.RedeemedDailyLoginDates.Contains(today);
        string? tokenId = FindDailyLoginTokenId(tokens, today);
        bool hasToken = tokenId is not null;

        return EarthJson(BuildDailyGoodiesResponse(today, tokenClaims, tokenId, hasToken, claimed));
    }

    [HttpPost("player/dailygoodies")]
    [HttpPost("player/daily-goodies")]
    [HttpPost("player/daily-login")]
    [HttpPost("player/dailyrewards")]
    [HttpPost("dailygoodies")]
    [HttpPost("daily-goodies")]
    [HttpPost("daily-login")]
    [HttpPost("dailyrewards")]
    [HttpPost("player/dailygoodies/claim")]
    [HttpPost("player/daily-goodies/claim")]
    [HttpPost("player/daily-login/claim")]
    [HttpPost("player/dailyrewards/claim")]
    [HttpPost("dailygoodies/claim")]
    [HttpPost("daily-goodies/claim")]
    [HttpPost("daily-login/claim")]
    [HttpPost("dailyrewards/claim")]
    [HttpPost("player/dailygoodies/collect")]
    [HttpPost("player/daily-goodies/collect")]
    [HttpPost("player/daily-login/collect")]
    [HttpPost("player/dailyrewards/collect")]
    [HttpPost("dailygoodies/collect")]
    [HttpPost("daily-goodies/collect")]
    [HttpPost("daily-login/collect")]
    [HttpPost("dailyrewards/collect")]
    [HttpPost("player/dailygoodies/redeem")]
    [HttpPost("player/daily-goodies/redeem")]
    [HttpPost("player/daily-login/redeem")]
    [HttpPost("player/dailyrewards/redeem")]
    [HttpPost("dailygoodies/redeem")]
    [HttpPost("daily-goodies/redeem")]
    [HttpPost("daily-login/redeem")]
    [HttpPost("dailyrewards/redeem")]
    public async Task<Results<ContentHttpResult, BadRequest>> Claim(CancellationToken cancellationToken)
    {
        if (!TryGetPlayerId(out string playerId))
        {
            return TypedResults.BadRequest();
        }

        await TokenUtils.EnsureDailyLoginToken(playerId, cancellationToken);

        long requestStartedOn = HttpContext.GetTimestamp();
        string today = TodayUtc();

        EarthDB.Results results = await new EarthDB.Query(true)
            .Get("tokens", playerId, typeof(Tokens))
            .Get("tokenClaims", playerId, typeof(TokenClaims))
            .Then(results1 =>
            {
                Tokens tokens = results1.Get<Tokens>("tokens");
                TokenClaims tokenClaims = results1.Get<TokenClaims>("tokenClaims");

                string? tokenId = FindDailyLoginTokenId(tokens, today);
                Tokens.Token? removedToken = tokenId is null ? null : tokens.RemoveToken(tokenId);
                if (removedToken is null)
                {
                    bool alreadyClaimed = tokenClaims.RedeemedDailyLoginDates.Contains(today);
                    return new EarthDB.Query(false)
                        .Extra("success", alreadyClaimed)
                        .Extra("alreadyClaimed", alreadyClaimed)
                        .Extra("tokenClaims", tokenClaims)
                        .Extra("tokens", tokens);
                }

                return new EarthDB.Query(true)
                    .Update("tokens", playerId, tokens)
                    .Then(TokenUtils.DoActionsOnRedeemedToken(removedToken, playerId, requestStartedOn, staticData), false)
                    .Extra("success", true)
                    .Extra("alreadyClaimed", false)
                    .Extra("tokenClaims", tokenClaims)
                    .Extra("tokens", tokens);
            })
            .ExecuteAsync(earthDB, cancellationToken);

        if (!(bool)results.GetExtra("success"))
        {
            return TypedResults.BadRequest();
        }

        TokenClaims latestClaims = (await new EarthDB.Query(false)
            .Get("tokenClaims", playerId, typeof(TokenClaims))
            .Get("tokens", playerId, typeof(Tokens))
            .ExecuteAsync(earthDB, cancellationToken))
            .Get<TokenClaims>("tokenClaims");

        var updates = new EarthApiResponse.UpdatesResponse(results);
        updates.Map["tokens"] = (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        return EarthJson(BuildDailyGoodiesResponse(today, latestClaims, null, false, true), updates);
    }

    private bool TryGetPlayerId(out string playerId)
    {
        playerId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
        return !string.IsNullOrEmpty(playerId);
    }

    private static string TodayUtc()
        => DateTimeOffset.UtcNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static string? FindDailyLoginTokenId(Tokens tokens, string today)
        => tokens.GetTokens()
            .FirstOrDefault(token => token.Token is Tokens.DailyLoginToken dailyLoginToken && dailyLoginToken.Date == today)
            ?.Id;

    private static Dictionary<string, object> BuildDailyGoodiesResponse(string today, TokenClaims tokenClaims, string? tokenId, bool hasToken, bool claimed)
    {
        DBRewards rewards = DailyLoginRewards();

        var rewardResponse = Utils.Rewards.FromDBRewardsModel(rewards).ToApiResponse();
        int streak = Math.Max(1, tokenClaims.DailyLoginStreak);
        int currentDay = ((streak - 1) % 7) + 1;
        string state = claimed ? "Completed" : hasToken ? "Available" : "Locked";

        return new Dictionary<string, object>
        {
            ["id"] = tokenId ?? "",
            ["date"] = today,
            ["state"] = state,
            ["claimed"] = claimed,
            ["available"] = hasToken && !claimed,
            ["streak"] = streak,
            ["currentDay"] = currentDay,
            ["tokenId"] = tokenId ?? "",
            ["rewards"] = rewardResponse,
            ["dailyGift"] = rewardResponse,
            ["dailyLoginBonuses"] = Enumerable.Range(1, 7).Select(day => new Dictionary<string, object>
            {
                ["day"] = day,
                ["state"] = day < currentDay || claimed && day == currentDay ? "Completed" : day == currentDay ? state : "Locked",
                ["claimed"] = day < currentDay || claimed && day == currentDay,
                ["available"] = day == currentDay && hasToken && !claimed,
                ["rewards"] = rewardResponse
            }).ToArray(),
            ["thingsToDoToday"] = new[]
            {
                new Dictionary<string, object> { ["challengeId"] = "bd9d3fd7-12ef-49e0-91fa-c971795f8e35", ["reward"] = 30 },
                new Dictionary<string, object> { ["challengeId"] = "1d981b84-a03a-451d-82a6-9bfe0fc885fb", ["reward"] = 45 },
                new Dictionary<string, object> { ["challengeId"] = "2619913d-6504-4c74-9fc9-e03649a70efc", ["reward"] = 50 }
            },
            ["calendar"] = new[]
            {
                new Dictionary<string, object>
                {
                    ["day"] = 1,
                    ["state"] = "Available",
                    ["rewards"] = rewardResponse
                }
            }
        };
    }

    private static DBRewards DailyLoginRewards()
        => new(0, 25, null, new Dictionary<string, int?> { [CommonAdventureCrystalId] = 1 }, [], []);
}
