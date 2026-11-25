using Immediate.Apis.Shared;
using Immediate.Handlers.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Solace.Db.Earth;
using Solace.WebPortal.Common;
using Solace.WebPortal.Common.Features.Seasons;

namespace Solace.WebPortal.Features.Seasons;

[Handler]
[MapGet("")]
[MapGroup<SeasonsGroup>]
[Authorize(Policy = Permissions.ViewSeasonsAndChallenges)]
public static partial class GetSeasons
{
    public sealed record Query;

    private static async ValueTask<List<SeasonPreviewDto>> HandleAsync(
        Query _,
        EarthDbContext earthDb,
        CancellationToken cancellationToken
    )
    {
        await SeasonUtils.DeactivateExpiredSeasonsAsync(earthDb, cancellationToken);

        var seasons = await earthDb.Seasons
            .AsNoTracking()
            .Include(season => season.Challenges)
            .OrderByDescending(season => season.SeasonNumber)
            .ToListAsync(cancellationToken);

        return await SeasonUtils.ToPreviewDtosAsync(earthDb, seasons, cancellationToken);
    }
}
