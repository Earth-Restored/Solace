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
using Solace.StaticData;
using DBRewards = Solace.DB.Models.Common.Rewards;

namespace Solace.ApiServer.Controllers.EarthApi;

[Authorize]
[ApiVersion("1.1")]
[Route("1/api/v{version:apiVersion}")]
internal sealed class DailyGoodiesController : SolaceControllerBase
{
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
        TokensEF.TokenWithId token = EnsureDailyLoginToken(tokens, today);
        await _earthDB.SaveChangesAsync(cancellationToken);

        var dailyLoginToken = (TokensEF.DailyLoginToken)token.Token;
        return EarthJson(BuildDailyGoodiesResponse(today, dailyLoginToken, token.Id));
    }

    [HttpPost("player/dailygoodies/claim")]
    [HttpPost("player/dailyrewards/claim")]
    [HttpPost("player/dailygoodies/collect")]
    [HttpPost("player/dailyrewards/collect")]
    [HttpPost("player/dailygoodies/redeem")]
    [HttpPost("player/dailyrewards/redeem")]
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

        TokensEF.TokenWithId? token = FindDailyLoginToken(tokens, today);
        if (token?.Token is not TokensEF.DailyLoginToken dailyLoginToken || dailyLoginToken.Claimed)
        {
            return TypedResults.BadRequest();
        }

        var claimedToken = new TokensEF.DailyLoginToken(dailyLoginToken.Date, dailyLoginToken.Rewards.DeepCopy(), true, requestStartedOn);
        tokens.AddToken(token.Id, claimedToken);

        await _earthDB.SaveChangesAsync(cancellationToken);

        var results = new EarthDbContext.Results(_earthDB) { Tokens = tokens.Version };
        await TokenUtils.DoActionsOnRedeemedTokenAsync(results, dailyLoginToken, accountId, requestStartedOn, _staticData);

        var updates = new EarthApiResponse.UpdatesResponse(results);
        return EarthJson(BuildDailyGoodiesResponse(today, claimedToken, null), updates);
    }

    private static string TodayUtc()
        => DateTimeOffset.UtcNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static TokensEF.TokenWithId EnsureDailyLoginToken(TokensEF tokens, string today)
    {
        TokensEF.TokenWithId? token = FindDailyLoginToken(tokens, today);
        if (token is not null)
        {
            return token;
        }

        string tokenId = Guid.NewGuid().ToString();
        var dailyLoginToken = new TokensEF.DailyLoginToken(today, DailyLoginRewards());
        tokens.AddToken(tokenId, dailyLoginToken);
        return new TokensEF.TokenWithId(tokenId, dailyLoginToken);
    }

    private static TokensEF.TokenWithId? FindDailyLoginToken(TokensEF tokens, string today)
        => tokens.GetTokens()
            .FirstOrDefault(token => token.Token is TokensEF.DailyLoginToken dailyLoginToken && dailyLoginToken.Date == today)
            is { Token: not null } token ? token : null;

    private static Dictionary<string, object> BuildDailyGoodiesResponse(string today, TokensEF.DailyLoginToken dailyLoginToken, string? tokenId)
    {
        DBRewards rewards = dailyLoginToken.Rewards;

        var rewardResponse = Utils.Rewards.FromDBRewardsModel(rewards).ToApiResponse();
        int streak = 1;
        int currentDay = ((streak - 1) % 7) + 1;
        bool claimed = dailyLoginToken.Claimed;
        bool hasToken = !claimed;
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
            ["dailyLoginBonuses"] = BuildDailyLoginBonuses(currentDay, state, hasToken, claimed, rewardResponse),
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

    private static Dictionary<string, object>[] BuildDailyLoginBonuses(int currentDay, string currentState, bool hasToken, bool claimed, object rewardResponse)
        => [.. Enumerable.Range(1, 7).Select(day =>
        {
            bool dayIsClaimed = day < currentDay || (claimed && day == currentDay);
            string dayState = dayIsClaimed ? "Completed" : day == currentDay ? currentState : "Locked";

            return new Dictionary<string, object>
            {
                ["day"] = day,
                ["state"] = dayState,
                ["claimed"] = dayIsClaimed,
                ["available"] = day == currentDay && hasToken && !claimed,
                ["rewards"] = rewardResponse
            };
        })];

    private static DBRewards DailyLoginRewards()
        => new(0, 25, null, new Dictionary<string, int?> { [AdventuresConfig.CommonAdventureCrystalId] = 1 }, [], []);
}
