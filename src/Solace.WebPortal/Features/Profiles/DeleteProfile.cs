using Immediate.Apis.Shared;
using Immediate.Handlers.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Solace.Db.Earth;
using Solace.WebPortal.Common;

namespace Solace.WebPortal.Features.Profiles;

[Handler]
[MapDelete("{profileId}")]
[MapGroup<ProfilesGroup>]
[Authorize(Policy = Permissions.CreateProfile)]
public static partial class DeleteProfile
{
    public sealed record Command([property: FromRoute] Guid ProfileId);

    private static async ValueTask<Results<Ok, NotFound, UnauthorizedHttpResult>> HandleAsync(
        Command command,
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

        var deleted = (await earthDb.Profiles
            .Where(profile => profile.Id == command.ProfileId && profile.WebPortalAccountId == userId)
            .ExecuteDeleteAsync(cancellationToken)) > 0;

        return deleted ? TypedResults.Ok() : TypedResults.NotFound();
    }
}
