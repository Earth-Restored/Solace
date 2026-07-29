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

namespace Solace.WebPortal.Features.Players;

[Handler]
[MapGet("{profileId}")]
[MapGroup<PlayersGroup>]
[Authorize(Policy = Permissions.ViewPlayers)]
public static partial class GetPlayer
{
    public sealed record Query([property: FromRoute] Guid ProfileId);

    private static async ValueTask<Results<Ok<PlayerDto>, NotFound>> HandleAsync(
        Query query,
        EarthDbContext earthDb,
        StaticDataProvider staticData,
        CancellationToken cancellationToken)
    {
        var utcNow = DateTimeOffset.UtcNow;

        var profile = await earthDb.Profiles
            .AsNoTracking()
            .Include(profile => profile.Boosts)
            .Select(profile => new { profile.Id, profile.Username, profile.Health, profile.Level, profile.Experience, PurchasedRubies = profile.Rubies.Purchased, EarnedRubies = profile.Rubies.Earned, profile.Boosts, })
            .FirstOrDefaultAsync(profile => profile.Id == query.ProfileId, cancellationToken);

        if (profile is null)
        {
            return TypedResults.NotFound();
        }

        var maxHealth = BoostUtils.GetMaxPlayerHealth(profile.Boosts!, utcNow, staticData.Catalog.ItemsCatalog);

        var buildplateCount = await earthDb.PlayerBuildplates
            .CountAsync(buildplate => buildplate.ProfileId == query.ProfileId, cancellationToken);

        return TypedResults.Ok(new PlayerDto(
            profile.Id,
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