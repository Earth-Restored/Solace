using Immediate.Apis.Shared;
using Immediate.Handlers.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Solace.Common.Utils;
using Solace.Db.Earth;
using Solace.StaticData;
using Solace.WebPortal.Common;
using Solace.WebPortal.Common.Features.Players;
using Solace.WebPortal.Data;

namespace Solace.WebPortal.Features.Players;

[Handler]
[MapGet("{profileId}")]
[MapGroup<PlayersGroup>]
[Authorize]
public static partial class GetPlayer
{
    public sealed record Query([property: FromRoute] Guid ProfileId);

    private static async ValueTask<Results<Ok<PlayerDto>, NotFound, UnauthorizedHttpResult, ForbidHttpResult>> HandleAsync(
        Query query,
        EarthDbContext earthDb,
        ApplicationDbContext appDb,
        StaticDataProvider staticData,
        IHttpContextAccessor httpContextAccessor,
        CancellationToken cancellationToken)
    {
        var httpUser = httpContextAccessor.HttpContext?.User;
        if (httpUser is null)
        {
            return TypedResults.Unauthorized();
        }

        var userId = httpUser.GetIdLong();

        var utcNow = DateTimeOffset.UtcNow;

        var profile = await earthDb.Profiles
            .AsNoTracking()
            .Include(profile => profile.Boosts)
            .Select(profile => new { profile.Id, profile.WebPortalAccountId, profile.Username, profile.Health, profile.Level, profile.Experience, PurchasedRubies = profile.Rubies.Purchased, EarnedRubies = profile.Rubies.Earned, profile.Boosts, })
            .FirstOrDefaultAsync(profile => profile.Id == query.ProfileId, cancellationToken);

        if (profile is null)
        {
            return TypedResults.NotFound();
        }

        if (!httpUser.HasPermission(Permissions.ViewPlayers) && profile.WebPortalAccountId != userId)
        {
            return TypedResults.Forbid();
        }

        var maxHealth = BoostUtils.GetMaxPlayerHealth(profile.Boosts!, utcNow, staticData.Catalog.ItemsCatalog);

        var buildplateCount = await earthDb.PlayerBuildplates
            .CountAsync(buildplate => buildplate.ProfileId == query.ProfileId, cancellationToken);

        var ownerUsername = profile.WebPortalAccountId is null
            ? null
            : (await appDb.Users
                .Select(user => new { user.Id, user.Email })
                .FirstOrDefaultAsync(user => user.Id == profile.WebPortalAccountId.Value, cancellationToken))?.Email;

        return TypedResults.Ok(new PlayerDto(
            profile.Id,
            ownerUsername,
            profile.WebPortalAccountId == userId,
            profile.Username,
            profile.Health,
            maxHealth,
            profile.Level,
            profile.Experience,
            PlayerUtils.GetLevelProgressPercentage(profile.Level, profile.Experience, staticData.Levels),
            profile.PurchasedRubies,
            profile.EarnedRubies,
            buildplateCount));
    }
}