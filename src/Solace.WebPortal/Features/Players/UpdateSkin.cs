using System.Diagnostics;
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
[Authorize(Policy = Permissions.ManagePlayers)]
public static partial class UpdateSkin
{
    public sealed record Command([property: FromRoute] Guid Id, [property: FromQuery] SkinType? SkinType);

    private static async ValueTask<Results<Ok, BadRequest<string>>> HandleAsync(
        Command command,
        EarthDbContext earthDb,
        IHttpContextAccessor httpContextAccessor,
        CancellationToken cancellationToken
    )
    {
        var httpContext = httpContextAccessor.HttpContext;
        Debug.Assert(httpContext is not null);

        using var imageStream = httpContext.Request.Body;

        var skinResult = await JavaSkinProcessor.Process(imageStream, command.SkinType ?? SkinType.Auto, cancellationToken);

        if (skinResult is string error)
        {
            return TypedResults.BadRequest(error);
        }

        var (skinImageData, isSkinSlim) = ((byte[], bool))skinResult.Value!;

        await earthDb.Profiles
           .Where(account => account.Id == command.Id)
           .ExecuteUpdateAsync(s => s
               .SetProperty(account => account.SkinImageData, skinImageData)
               .SetProperty(account => account.IsSkinSlim, isSkinSlim),
               cancellationToken);

        return TypedResults.Ok();
    }
}
