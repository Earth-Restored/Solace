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
[MapGet("{id}")]
[MapGroup<PlayersGroup>]
[Authorize(Policy = Permissions.ViewPlayers)]
public static partial class GetPlayer
{
    public sealed record Query([property: FromRoute] Guid Id);

    private static async ValueTask<Results<Ok<PlayerDto>, NotFound>> HandleAsync(
        Query query,
        EarthDbContext earthDb,
        StaticDataProvider staticData,
        CancellationToken cancellationToken)
    {
        var utcNow = DateTimeOffset.UtcNow;

        var player = await earthDb.Accounts
            .AsNoTracking()
            .Include(account => account.Profile)
            .Include(account => account.Boosts)
            .Select(account => new { account.Id, account.Username, account.Profile!.Health, account.Profile.Level, account.Profile.Experience, PurchasedRubies = account.Profile.Rubies.Purchased, EarnedRubies = account.Profile.Rubies.Earned, account.Boosts, })
            .FirstOrDefaultAsync(account => account.Id == query.Id, cancellationToken);

        if (player is null)
        {
            return TypedResults.NotFound();
        }

        var maxHealth = BoostUtils.GetMaxPlayerHealth(player.Boosts!, utcNow, staticData.Catalog.ItemsCatalog);

        var buildplateCount = await earthDb.PlayerBuildplates
            .CountAsync(buildplate => buildplate.AccountId == query.Id, cancellationToken);

        return TypedResults.Ok(new PlayerDto(
            player.Id,
            player.Username,
            player.Health,
            maxHealth,
            player.Level,
            player.Experience,
            PlayerUtils.GetLevelProgressPercentage(player.Level, player.Experience, staticData.Levels),
            player.PurchasedRubies,
            player.EarnedRubies,
            buildplateCount));
    }
}