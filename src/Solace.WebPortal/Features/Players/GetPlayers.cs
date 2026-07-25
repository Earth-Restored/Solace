using Immediate.Apis.Shared;
using Immediate.Handlers.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Solace.Common.Utils;
using Solace.Db.Earth;
using Solace.StaticData;
using Solace.WebPortal.Common;
using Solace.WebPortal.Common.Features.Players;

namespace Solace.WebPortal.Features.Players;

[Handler]
[MapGet("")]
[MapGroup<PlayersGroup>]
[Authorize(Policy = Permissions.ViewPlayers)]
public static partial class GetPlayers
{
    public sealed record Query(
        string? SearchTerm = null,
        int Page = 1,
        int PageSize = 10
    );
    private static async ValueTask<GetPlayersResponse> HandleAsync(
        Query query,
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

        if (!string.IsNullOrWhiteSpace(query.SearchTerm))
        {
            var search = $"%{query.SearchTerm}%";

            dbQuery = dbQuery.Where(account =>
                (account.Username != null && EF.Functions.ILike(account.Username, search)) ||
                EF.Functions.ILike(account.Id.ToString(), search));
        }

        var totalPlayers = await dbQuery.CountAsync(cancellationToken);
        var totalPages = (int)double.Ceiling(totalPlayers / (double)query.PageSize);
        var page = int.Max(1, int.Min(query.Page, int.Max(1, totalPages)));

        var players = dbQuery
            .OrderBy(u => u.Username)
            .Skip((page - 1) * query.PageSize)
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

        return new GetPlayersResponse(
            playerDtos,
            totalPlayers,
            totalPages,
            page
        );
    }
}