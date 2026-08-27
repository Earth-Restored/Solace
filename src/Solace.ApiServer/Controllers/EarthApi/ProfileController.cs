using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Serilog;
using System.Security.Claims;
using Solace.ApiServer.Utils;
using Solace.Common;
using Solace.Common.Utils;
using Solace.DB;
using Solace.DB.Models.Player;
using Solace.StaticData;
using Microsoft.EntityFrameworkCore;
using Solace.DB.Utils;

namespace Solace.ApiServer.Controllers.EarthApi;

[Authorize]
[ApiVersion("1.1")]
[Route("1/api/v{version:apiVersion}/player")]
internal sealed class ProfileController : SolaceControllerBase
{
    private readonly EarthDbContext _earthDB;
    private readonly StaticData.StaticData _staticData;

    public ProfileController(EarthDbContext earthDB, StaticData.StaticData staticData)
    {
        _earthDB = earthDB;
        _staticData = staticData;
    }

    [HttpGet("profile/{userId}")]
    public async Task<Results<ContentHttpResult, BadRequest>> GetProfile(string userId, CancellationToken cancellationToken)
    {
        // TODO: decide if we should allow requests for profiles of other players
        if (!Guid.TryParse(userId, out var accountId))
        {
            return TypedResults.BadRequest();
        }

        // request.timestamp
        long requestStartedOn = HttpContext.GetTimestamp();

        var profile = await _earthDB.Profiles
            .AsNoTracking()
            .FirstOrNewAsync(profile => profile.Id == accountId, trackNew: false, cancellationToken: cancellationToken);

        var boosts = await _earthDB.Boosts
            .AsNoTracking()
            .FirstOrNewAsync(boosts => boosts.Id == accountId, trackNew: false, cancellationToken: cancellationToken);

        var levels = _staticData.Levels.Levels;
        int currentLevelExperience = profile.Experience - (profile.Level > 1 ? profile.Level - 2 < levels.Length ? levels[profile.Level - 2].ExperienceRequired : levels[^1].ExperienceRequired : 0);
        int experienceRemaining = profile.Level - 1 < levels.Length ? levels[profile.Level - 1].ExperienceRequired - profile.Experience : 0;

        int maxPlayerHealth = BoostUtils.GetMaxPlayerHealth(boosts, requestStartedOn, _staticData.Catalog.ItemsCatalog);
        if (profile.Health > maxPlayerHealth)
        {
            profile.Health = maxPlayerHealth;
        }

        string resp = Json.Serialize(new EarthApiResponse(new Types.Profile.Profile(
            Enumerable.Range(0, levels.Length).Select(levelIndex =>
            {
                var level = levels[levelIndex];
                return new KeyValuePair<int, Types.Profile.Profile.LevelR>(levelIndex + 1, new(level.ExperienceRequired, LevelUtils.MakeLevelRewards(level).ToApiResponse()));
            }).ToDictionary(),
            profile.Experience,
            profile.Level,
            currentLevelExperience,
            experienceRemaining,
            profile.Health,
            profile.Health / (float)maxPlayerHealth * 100.0f)));

        return TypedResults.Content(resp, "application/json");
    }

    [ResponseCache(Duration = 11200)]
    [HttpGet("rubies")]
    public async Task<Results<ContentHttpResult, BadRequest>> GetRubies(CancellationToken cancellationToken)
    {
        if (!TryGetAccountId(out var accountId))
        {
            return TypedResults.BadRequest();
        }

        var profile = await _earthDB.Profiles
            .AsNoTracking()
            .FirstOrNewAsync(profile => profile.Id == accountId, trackNew: false, cancellationToken: cancellationToken);

        string resp = Json.Serialize(new EarthApiResponse(profile.Rubies.Purchased + profile.Rubies.Earned));
        return TypedResults.Content(resp, "application/json");
    }

    [ResponseCache(Duration = 11200)]
    [HttpGet("splitRubies")]
    public async Task<Results<ContentHttpResult, BadRequest>> GetSplitRubies(CancellationToken cancellationToken)
    {
        if (!TryGetAccountId(out var accountId))
        {
            return TypedResults.BadRequest();
        }

        var profile = await _earthDB.Profiles
            .AsNoTracking()
            .FirstOrNewAsync(profile => profile.Id == accountId, trackNew: false, cancellationToken: cancellationToken);

        string resp = Json.Serialize(new EarthApiResponse(new Types.Profile.SplitRubies(profile.Rubies.Purchased, profile.Rubies.Earned)));
        return TypedResults.Content(resp, "application/json");
    }

    // required for the language selection option in the client to work
    [HttpPost("profile/language")]
    public Ok ChangeLanguage()
        => TypedResults.Ok();
}
