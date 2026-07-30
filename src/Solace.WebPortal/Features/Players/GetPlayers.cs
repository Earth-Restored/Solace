using Immediate.Apis.Shared;
using Immediate.Handlers.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Solace.Common.Utils;
using Solace.Db.Earth;
using Solace.StaticData;
using Solace.WebPortal.Common;
using Solace.WebPortal.Common.Features.Common;
using Solace.WebPortal.Common.Features.Players;

namespace Solace.WebPortal.Features.Players;

[Handler]
[MapGet("")]
[MapGroup<PlayersGroup>]
[Authorize(Policy = Permissions.ViewPlayers)]
public static partial class GetPlayers
{
    private static async ValueTask<PagedSearchResult<List<PlayerDto>>> HandleAsync(
        PagedSearchQuery query,
        EarthDbContext earthDb,
        StaticDataProvider staticData,
        CancellationToken cancellationToken)
    {
        var utcNow = DateTimeOffset.UtcNow;

        var dbQuery = earthDb.Profiles
            .AsNoTracking()
            .Include(profile => profile.Boosts)
            .Select(profile => new { profile.Id, profile.Username, profile.Health, profile.Level, profile.Experience, PurchasedRubies = profile.Rubies.Purchased, EarnedRubies = profile.Rubies.Earned, profile.Boosts, });

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
            var maxHealth = BoostUtils.GetMaxPlayerHealth(player.Boosts!, utcNow, staticData.Catalog.ItemsCatalog);

            playerDtos.Add(new PlayerDto(
                player.Id,
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

        return new(playerDtos, totalCount, matchingCount);
    }
}