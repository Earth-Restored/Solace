using Immediate.Apis.Shared;
using Immediate.Handlers.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Solace.Db.Earth;
using Solace.EventBus.Client;
using Solace.WebPortal.Common;
using Solace.WebPortal.Common.Features.Seasons;

namespace Solace.WebPortal.Features.Seasons;

[Handler]
[MapPut("{seasonId:guid}/activation")]
[MapGroup<SeasonsGroup>]
[Authorize(Policy = Permissions.EditSeasonsAndChallenges)]
public static partial class UpdateSeasonActivation
{
    public sealed record Command([property: FromRoute] Guid SeasonId, [property: FromBody] UpdateSeasonActivationCommand Body);

    private static async ValueTask<Results<Ok<SeasonPreviewDto>, BadRequest<string>, NotFound>> HandleAsync(
        Command command,
        EarthDbContext earthDb,
        EventBusClient eventBus,
        CancellationToken cancellationToken
    )
    {
        var body = command.Body;

        if (body.IsActive)
        {
            if (body.StartDate is null || body.EndDate is null)
            {
                return TypedResults.BadRequest("Start date and end date are required to enable a season.");
            }

            if (body.EndDate <= body.StartDate)
            {
                return TypedResults.BadRequest("End date must be after start date.");
            }

            if (body.EndDate <= DateTimeOffset.UtcNow)
            {
                return TypedResults.BadRequest("End date must be in the future.");
            }
        }

        var season = await earthDb.Seasons
            .AsTracking()
            .Include(season => season.Challenges)
            .FirstOrDefaultAsync(season => season.Id == command.SeasonId, cancellationToken);

        if (season is null)
        {
            return TypedResults.NotFound();
        }

        if (body.IsActive)
        {
            var otherActiveSeasons = await earthDb.Seasons
                .AsTracking()
                .Where(season => season.Id != season.Id && season.IsActive)
                .ToListAsync(cancellationToken);

            foreach (var other in otherActiveSeasons)
            {
                other.IsActive = false;
                other.StartDate = null;
                other.EndDate = null;
            }

            season.IsActive = true;
            season.StartDate = body.StartDate!.Value.ToUniversalTime();
            season.EndDate = body.EndDate!.Value.ToUniversalTime();
        }
        else
        {
            season.IsActive = false;
            season.StartDate = null;
            season.EndDate = null;
        }

        await earthDb.SaveChangesAsync(cancellationToken);
        await eventBus.PublishAsync("seasons", "seasons_data_updated", string.Empty, cancellationToken);

        return TypedResults.Ok(await SeasonUtils.ToPreviewDtoAsync(earthDb, season, cancellationToken));
    }
}
