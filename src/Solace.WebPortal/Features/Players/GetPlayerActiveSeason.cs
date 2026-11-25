using Immediate.Apis.Shared;
using Immediate.Handlers.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Solace.Db.Earth;
using Solace.WebPortal.Common;
using Solace.WebPortal.Common.Features.Seasons;
using Solace.WebPortal.Features.Seasons;

namespace Solace.WebPortal.Features.Players;

[Handler]
[MapGet("{profileId:guid}/season")]
[MapGroup<PlayersGroup>]
[Authorize]
public static partial class GetPlayerActiveSeason
{
    public sealed record Query([property: FromRoute] Guid ProfileId);

    private static async ValueTask<Results<Ok<PlayerActiveSeasonDto>, NotFound, UnauthorizedHttpResult, ForbidHttpResult>> HandleAsync(
        Query query,
        EarthDbContext earthDb,
        IHttpContextAccessor httpContextAccessor,
        CancellationToken cancellationToken
    )
    {
        var httpUser = httpContextAccessor.HttpContext?.User;
        if (httpUser is null)
        {
            return TypedResults.Unauthorized();
        }

        var userId = httpUser.GetIdLong();

        var profile = await earthDb.Profiles
            .AsNoTracking()
            .Select(profile => new { profile.Id, profile.WebPortalAccountId, })
            .FirstOrDefaultAsync(profile => profile.Id == query.ProfileId, cancellationToken);

        if (profile is null)
        {
            return TypedResults.NotFound();
        }

        if (!httpUser.HasPermission(Permissions.ViewPlayers) && profile.WebPortalAccountId != userId)
        {
            return TypedResults.Forbid();
        }

        await SeasonUtils.DeactivateExpiredSeasonsAsync(earthDb, cancellationToken);

        var season = await earthDb.Seasons
            .AsNoTracking()
            .Include(season => season.Challenges)
            .FirstOrDefaultAsync(season => season.IsActive, cancellationToken);

        if (season is null)
        {
            return TypedResults.Ok(new PlayerActiveSeasonDto(null, null));
        }

        var seasonDto = await SeasonUtils.ToPreviewDtoAsync(earthDb, season, cancellationToken);

        var seasonProgress = await earthDb.PlayerSeasonProgresses
            .AsNoTracking()
            .FirstOrDefaultAsync(progress => progress.ProfileId == query.ProfileId && progress.SeasonId == season.Id, cancellationToken);

        var challengeIds = season.Challenges.Select(challenge => challenge.Id).ToHashSet();
        var challengeProgressList = await earthDb.PlayerChallengeProgresses
            .AsNoTracking()
            .Where(progress => progress.ProfileId == query.ProfileId && challengeIds.Contains(progress.ChallengeId))
            .ToListAsync(cancellationToken);

        var challengeProgressMap = challengeProgressList.ToDictionary(
            progress => progress.ChallengeId,
            progress => new PlayerChallengeProgressDto(progress.CurrentCount, progress.IsComplete));

        var progressDto = new PlayerSeasonProgressDto(
            seasonProgress?.ActiveSeasonChallengeId,
            seasonProgress?.IsCompleted ?? false,
            seasonProgress?.StartedAt,
            seasonProgress?.CompletedAt,
            challengeProgressMap
        );

        return TypedResults.Ok(new PlayerActiveSeasonDto(seasonDto, progressDto));
    }
}
