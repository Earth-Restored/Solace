using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Solace.ApiServer.Utils;
using Solace.Db.Earth;
using Solace.Db.Earth.Models.Player;
using Microsoft.EntityFrameworkCore;
using Solace.StaticData;
using System.Text.Json.Serialization;
using DBRewards = Solace.Db.Earth.Models.Common.Rewards;

namespace Solace.ApiServer.Controllers;

[Authorize]
[ApiVersion("1.1")]
[Route("1/api/v{version:apiVersion}")]
internal sealed class DailyGoodiesController : SolaceControllerBase
{
    private readonly EarthDbContext _earthDb;
    private readonly StaticDataProvider _staticData;

    public DailyGoodiesController(EarthDbContext earthDB, StaticDataProvider staticData)
    {
        _earthDb = earthDB;
        _staticData = staticData;
    }

    // todo: surely only 1 is needed
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

        var today = TodayUtc();
        var dailyLoginToken = await EnsureDailyLoginTokenAsync(_earthDb, accountId, today, cancellationToken);
        await _earthDb.SaveChangesAsync(cancellationToken);

        return EarthJson(BuildDailyGoodiesResponse(today, dailyLoginToken, dailyLoginToken.TokenId));
    }

    // todo: surely only 1 is needed
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

        var requestStartedOn = HttpContext.GetTimestamp();
        var today = TodayUtc();

        var dailyLoginToken = await FindDailyLoginTokenAsync(_earthDb, accountId, today, cancellationToken);
        if (dailyLoginToken is null or { Claimed: true, })
        {
            return TypedResults.BadRequest();
        }

        var claimedToken = new DailyLoginTokenEF(accountId, dailyLoginToken.Date, dailyLoginToken.Rewards.DeepCopy(), requestStartedOn);
        _earthDb.Tokens.Add(claimedToken);

        await _earthDb.SaveChangesAsync(cancellationToken);

        var results = new ResultsEF.Builder()
            .Tokens();

        await TokenUtils.DoActionsOnRedeemedTokenAsync(_earthDb, results, dailyLoginToken, accountId, requestStartedOn, _staticData);

        var updates = new EarthApiResponse.UpdatesResponse(await results.BuildAsync(_earthDb, accountId, cancellationToken));
        return EarthJson(BuildDailyGoodiesResponse(today, claimedToken, null), updates);
    }

    private static DateOnly TodayUtc()
        => DateOnly.FromDateTime(DateTime.UtcNow);

    private static async Task<DailyLoginTokenEF> EnsureDailyLoginTokenAsync(EarthDbContext earthDb, Guid accountId, DateOnly today, CancellationToken cancellationToken = default)
    {
        var token = await FindDailyLoginTokenAsync(earthDb, accountId, today, cancellationToken);
        if (token is not null)
        {
            return token;
        }

        var dailyLoginToken = new DailyLoginTokenEF(accountId, today, DailyLoginRewards());
        earthDb.Tokens.Add(dailyLoginToken);
        await earthDb.SaveChangesAsync(cancellationToken);
        return dailyLoginToken;
    }

    private static async Task<DailyLoginTokenEF?> FindDailyLoginTokenAsync(EarthDbContext earthDb, Guid accountId, DateOnly today, CancellationToken cancellationToken = default)
        => await earthDb.Tokens
            .OfType<DailyLoginTokenEF>()
            .FirstOrDefaultAsync(token => token.ProfileId == accountId && token.Date == today, cancellationToken);

    public sealed record DailyGoodiesResponse(
        Guid? Id,
        DateOnly Date,
        DailyGoodiesState State,
        bool Claimed,
        bool Available,
        int Streak,
        int CurrentDay,
        Guid? TokenId,
        Types.Common.Rewards Rewards, // todo: surely only 1 is needed
        Types.Common.Rewards DailyGift,
        DailyLoginBonus[] DailyLoginBonuses,
        ThingToDo[] ThingsToDoToday,
        CalendarDay[] Calendar
    );

    [JsonConverter(typeof(JsonStringEnumConverter<DailyGoodiesState>))]
    public enum DailyGoodiesState
    {
        Locked,
        Available,
        Completed,
    }

    public sealed record DailyLoginBonus(
        int Day,
        DailyGoodiesState State,
        bool Claimed,
        bool Available,
        Types.Common.Rewards Rewards
    );

    public sealed record ThingToDo(
        Guid ChallengeId,
        int Reward
    );

    public sealed record CalendarDay(
        int Day,
        DailyGoodiesState State,
        Types.Common.Rewards Rewards
    );

    private static DailyGoodiesResponse BuildDailyGoodiesResponse(DateOnly today, DailyLoginTokenEF dailyLoginToken, Guid? tokenId)
    {
        var rewards = dailyLoginToken.Rewards;

        var rewardResponse = Utils.Rewards.FromDBRewardsModel(rewards).ToApiResponse();
        var streak = 1;
        var currentDay = ((streak - 1) % 7) + 1;
        var claimed = dailyLoginToken.Claimed;
        var hasToken = !claimed;
        var state = claimed ? DailyGoodiesState.Completed : hasToken ? DailyGoodiesState.Available : DailyGoodiesState.Locked;

        return new DailyGoodiesResponse(
            tokenId,
            today,
            state,
            claimed,
            hasToken && !claimed,
            streak,
            currentDay,
            tokenId,
            rewardResponse,
            rewardResponse,
            BuildDailyLoginBonuses(currentDay, state, hasToken, claimed, rewardResponse),
            [
                new ThingToDo(Guid.Parse("bd9d3fd7-12ef-49e0-91fa-c971795f8e35"), 30),
                new ThingToDo(Guid.Parse("1d981b84-a03a-451d-82a6-9bfe0fc885fb"), 45),
                new ThingToDo(Guid.Parse("2619913d-6504-4c74-9fc9-e03649a70efc"), 50),
            ],
            [
                new CalendarDay(1, DailyGoodiesState.Available, rewardResponse),
            ]
        );
    }

    private static DailyLoginBonus[] BuildDailyLoginBonuses(int currentDay, DailyGoodiesState currentState, bool hasToken, bool claimed, Types.Common.Rewards rewardResponse)
        => [.. Enumerable.Range(1, 7).Select(day =>
        {
            var dayIsClaimed = day < currentDay || (claimed && day == currentDay);
            var dayState = dayIsClaimed ? DailyGoodiesState.Completed : day == currentDay ? currentState : DailyGoodiesState.Locked;

            return new DailyLoginBonus(day, dayState, dayIsClaimed, day == currentDay && hasToken && !claimed, rewardResponse);
        })];

    private static DBRewards DailyLoginRewards()
        => new(0, 25, null, new Dictionary<Guid, int> { [AdventuresConfig.CommonAdventureCrystalId] = 1 }, [], []);
}
