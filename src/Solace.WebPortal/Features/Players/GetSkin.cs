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
[MapGet("{profileId}/skin")]
[MapGroup<PlayersGroup>]
[Authorize]
public static partial class GetSkin
{
    public sealed record Query([property: FromRoute] Guid ProfileId);

    private static async ValueTask<Results<Ok<SkinDto>, NoContent, NotFound, UnauthorizedHttpResult, ForbidHttpResult>> HandleAsync(
        Query query,
        EarthDbContext earthDb,
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

        var profile = await earthDb.Profiles
            .AsNoTracking()
            .Include(profile => profile.Boosts)
            .Select(profile => new { profile.Id, profile.WebPortalAccountId, profile.SkinImageData, profile.IsSkinSlim, })
            .FirstOrDefaultAsync(profile => profile.Id == query.ProfileId, cancellationToken);

        if (profile is null)
        {
            return TypedResults.NotFound();
        }

        if (!httpUser.HasPermission(Permissions.ViewPlayers) && profile.WebPortalAccountId != userId)
        {
            return TypedResults.Forbid();
        }

        if (profile.SkinImageData is null)
        {
            return TypedResults.NoContent();
        }

        return TypedResults.Ok(new SkinDto(profile.SkinImageData, profile.IsSkinSlim));
    }
}
