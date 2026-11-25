using Immediate.Apis.Shared;
using Immediate.Handlers.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Solace.Db.Earth;
using Solace.WebPortal.Common;
using Solace.WebPortal.Common.Features.Seasons;

namespace Solace.WebPortal.Features.Seasons;

[Handler]
[MapGet("{seasonId:guid}")]
[MapGroup<SeasonsGroup>]
[Authorize(Policy = Permissions.ViewSeasonsAndChallenges)]
public static partial class GetSeason
{
    public sealed record Query([property: FromRoute] Guid SeasonId);

    private static async ValueTask<Results<Ok<SeasonPreviewDto>, NotFound>> HandleAsync(
        Query query,
        EarthDbContext earthDb,
        CancellationToken cancellationToken
    )
    {
        await SeasonUtils.DeactivateExpiredSeasonsAsync(earthDb, cancellationToken);

        var season = await earthDb.Seasons
            .AsNoTracking()
            .Include(season => season.Challenges)
            .FirstOrDefaultAsync(season => season.Id == query.SeasonId, cancellationToken);

        if (season is null)
        {
            return TypedResults.NotFound();
        }

        var dto = await SeasonUtils.ToPreviewDtoAsync(earthDb, season, cancellationToken);
        return TypedResults.Ok(dto);
    }
}
