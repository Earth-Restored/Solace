using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using System.Globalization;
using Solace.ApiServer.Utils;
using Solace.DB;
using Solace.DB.Models.Player;
using Solace.DB.Utils;
using Microsoft.EntityFrameworkCore;
using DBRewards = Solace.DB.Models.Common.Rewards;

namespace Solace.ApiServer.Controllers.EarthApi;

[Authorize]
[ApiVersion("1.1")]
[Route("1/api/v{version:apiVersion}")]
internal sealed class DailyGoodiesController : SolaceControllerBase
{
    private const string CommonAdventureCrystalId = "4f16a053-4929-263a-c91a-29663e29df76";
    private readonly EarthDbContext _earthDB;
    private readonly StaticData.StaticData _staticData;

    public DailyGoodiesController(EarthDbContext earthDB, StaticData.StaticData staticData)
    {
        _earthDB = earthDB;
        _staticData = staticData;
    }

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
        if (!TryGetAccountId(out var accountId))
        {
            return TypedResults.BadRequest();
        }

        string today = TodayUtc();
        var tokens = await _earthDB.Tokens
            .AsTracking()
            .FirstOrNewAsync(tokens => tokens.Id == accountId, cancellationToken: cancellationToken);
        string tokenId = EnsureDailyLoginToken(tokens, today);
        await _earthDB.SaveChangesAsync(cancellationToken);

        TokenClaims tokenClaims = new() { DailyLoginStreak = 1 };
        bool claimed = tokenClaims.RedeemedDailyLoginDates.Contains(today);
        bool hasToken = true;

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
        if (!TryGetAccountId(out var accountId))
        {
            return TypedResults.BadRequest();
        }

        long requestStartedOn = HttpContext.GetTimestamp();
        string today = TodayUtc();

        var tokens = await _earthDB.Tokens
            .AsTracking()
            .FirstOrNewAsync(tokens => tokens.Id == accountId, cancellationToken: cancellationToken);

        string? tokenId = FindDailyLoginTokenId(tokens, today);
        if (tokenId is null)
        {
            return TypedResults.BadRequest();
        }

        TokensEF.Token? removedToken = tokens.RemoveToken(tokenId);
        if (removedToken is null)
        {
            return TypedResults.BadRequest();
        }

        await _earthDB.SaveChangesAsync(cancellationToken);

        var results = new EarthDbContext.Results(_earthDB) { Tokens = tokens.Version };
        await TokenUtils.DoActionsOnRedeemedTokenAsync(results, removedToken, accountId, requestStartedOn, _staticData);

        TokenClaims latestClaims = new()
        {
            DailyLoginStreak = 1,
            RedeemedDailyLoginDates = [today]
        };

        var updates = new EarthApiResponse.UpdatesResponse(results);
        return EarthJson(BuildDailyGoodiesResponse(today, latestClaims, null, false, true), updates);
    }

    private static string TodayUtc()
        => DateTimeOffset.UtcNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static string EnsureDailyLoginToken(TokensEF tokens, string today)
    {
        string? tokenId = FindDailyLoginTokenId(tokens, today);
        if (tokenId is not null)
        {
            return tokenId;
        }

        tokenId = Guid.NewGuid().ToString();
        tokens.AddToken(tokenId, new TokensEF.DailyLoginToken(today, DailyLoginRewards()));
        return tokenId;
    }

    private static string? FindDailyLoginTokenId(TokensEF tokens, string today)
        => tokens.GetTokens()
            .FirstOrDefault(token => token.Token is TokensEF.DailyLoginToken dailyLoginToken && dailyLoginToken.Date == today)
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
