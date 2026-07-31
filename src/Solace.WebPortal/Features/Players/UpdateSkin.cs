using System.Diagnostics;
using System.Globalization;
using System.Security.Claims;
using Immediate.Apis.Shared;
using Immediate.Handlers.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Solace.Db.Earth;
using Solace.WebPortal.Common;
using Solace.WebPortal.Common.Features.Players;

namespace Solace.WebPortal.Features.Players;

[Handler]
[MapPut("{id}/skin")]
[MapGroup<PlayersGroup>]
[Authorize]
public static partial class UpdateSkin
{
    public sealed record Command([property: FromRoute] Guid Id, [property: FromQuery] SkinType? SkinType);

    private static async ValueTask<Results<Ok, NotFound, BadRequest<string>, UnauthorizedHttpResult, ForbidHttpResult>> HandleAsync(
        Command command,
        EarthDbContext earthDb,
        IHttpContextAccessor httpContextAccessor,
        CancellationToken cancellationToken
    )
    {
        var httpContext = httpContextAccessor.HttpContext;
        Debug.Assert(httpContext is not null);

        var httpUser = httpContext.User;
        if (httpUser is null)
        {
            return TypedResults.Unauthorized();
        }

        if (!httpUser.HasPermission(Permissions.ManagePlayers))
        {
            var profile = await earthDb.Profiles
                .AsNoTracking()
                .Select(profile => new { profile.Id, profile.WebPortalAccountId })
                .FirstOrDefaultAsync(profile => profile.Id == command.Id, cancellationToken);

            var userId = httpUser.GetIdLong();

            if (profile is null)
            {
                return TypedResults.NotFound();
            }

            if (profile.WebPortalAccountId != userId)
            {
                return TypedResults.Forbid();
            }
        }

        using var imageStream = httpContext.Request.Body;

        var skinResult = await JavaSkinProcessor.Process(imageStream, command.SkinType ?? SkinType.Auto, cancellationToken);

        if (skinResult is string error)
        {
            return TypedResults.BadRequest(error);
        }

        var (skinImageData, isSkinSlim) = ((byte[], bool))skinResult.Value!;

        var updated = await earthDb.Profiles
           .Where(account => account.Id == command.Id)
           .ExecuteUpdateAsync(s => s
               .SetProperty(account => account.SkinImageData, skinImageData)
               .SetProperty(account => account.IsSkinSlim, isSkinSlim),
               cancellationToken);

        return updated > 0 ? TypedResults.Ok() : TypedResults.NotFound();
    }
}
