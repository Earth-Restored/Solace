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

        var dbQuery = earthDb.Accounts
            .AsNoTracking()
            .Include(account => account.Profile)
            .Include(account => account.Boosts)
            .Select(account => new { account.Id, account.Username, account.Profile!.Health, account.Profile.Level, account.Profile.Experience, PurchasedRubies = account.Profile.Rubies.Purchased, EarnedRubies = account.Profile.Rubies.Earned, account.Boosts, });

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
                PlayerUtils.GetLevelProgressPercentage(player.Level, player.Experience, staticData.Levels),
                player.PurchasedRubies,
                player.EarnedRubies));
        }

        return new(playerDtos, totalCount, matchingCount);
    }
}