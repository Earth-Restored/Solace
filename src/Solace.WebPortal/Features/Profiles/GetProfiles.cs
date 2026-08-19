using Immediate.Apis.Shared;
using Immediate.Handlers.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Solace.Db.Earth;
using Solace.WebPortal.Common;
using Solace.WebPortal.Common.Features.Profiles;

namespace Solace.WebPortal.Features.Profiles;

[Handler]
[MapGet("")]
[MapGroup<ProfilesGroup>]
[Authorize(Policy = Permissions.CreateProfile)]
public static partial class GetProfiles
{
    public sealed record Query;

    private static async ValueTask<Results<Ok<List<ProfileDto>>, UnauthorizedHttpResult>> HandleAsync(
        Query _,
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

        var profiles = await earthDb.Profiles
            .AsNoTracking()
            .Where(profile => profile.WebPortalAccountId == userId)
            .OrderBy(profile => profile.Username)
            .Select(profile => new ProfileDto(profile.Id, profile.Username))
            .ToListAsync(cancellationToken);

        return TypedResults.Ok(profiles);
    }
}
