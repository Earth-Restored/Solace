using Immediate.Apis.Shared;
using Immediate.Handlers.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Solace.Common.Utils;
using Solace.Db.Earth;
using Solace.StaticData;
using Solace.WebPortal.Common;
using Solace.WebPortal.Common.Features.Common;
using Solace.WebPortal.Common.Features.Players;

namespace Solace.WebPortal.Features.Players;

[Handler]
[MapGet("unlinked")]
[MapGroup<PlayersGroup>]
[Authorize(Policy = Permissions.ViewPlayers)]
public static partial class GetUnlinkedPlayers
{
    private static async ValueTask<Results<Ok<PagedSearchResult<List<PlayerDto>>>, UnauthorizedHttpResult>> HandleAsync(
        PagedSearchQuery query,
        EarthDbContext earthDb,
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

        var dbQuery = earthDb.Profiles
            .AsNoTracking()
            .Where(profile => profile.WebPortalAccountId == null)
            .Include(profile => profile.Boosts)
            .Select(profile => new { profile.Id, profile.WebPortalAccountId, profile.Username, profile.Health, profile.Level, profile.Experience, PurchasedRubies = profile.Rubies.Purchased, EarnedRubies = profile.Rubies.Earned, profile.Boosts, });

        var totalCount = await dbQuery.CountAsync(cancellationToken);
        int matchingCount;

        if (!string.IsNullOrWhiteSpace(query.SearchTerm))
        {
            var search = $"%{query.SearchTerm}%";

            dbQuery = dbQuery.Where(account =>
                (account.Username != null && EF.Functions.ILike(account.Username, search)) ||
                EF.Functions.ILike(account.Id.ToString(), search));

            matchingCount = await dbQuery.CountAsync(cancellationToken);
        }
        else
        {
            matchingCount = totalCount;
        }

        var players = dbQuery
            .OrderBy(u => u.Username)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize);

        var playerDtos = new List<PlayerDto>(query.PageSize);
        foreach (var player in players)
        {
            var maxHealth = BoostUtils.GetMaxPlayerHealth(player.Boosts!, utcNow, staticData.Catalog);

            playerDtos.Add(new PlayerDto(
                player.Id,
                null,
                player.WebPortalAccountId == userId,
                player.Username,
                player.Health,
                maxHealth,
                player.Level,
                player.Experience,
                PlayerUtils.GetLevelProgressPercentage(player.Level, player.Experience, staticData.Levels),
                player.PurchasedRubies,
                player.EarnedRubies,
                null));
        }

        return TypedResults.Ok(new PagedSearchResult<List<PlayerDto>>(playerDtos, totalCount, matchingCount));
    }
}