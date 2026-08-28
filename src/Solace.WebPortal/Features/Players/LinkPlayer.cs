using Immediate.Apis.Shared;
using Immediate.Handlers.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Solace.Db.Earth;
using Solace.WebPortal.Common;
using Solace.WebPortal.Data;

namespace Solace.WebPortal.Features.Players;

[Handler]
[MapPost("{profileId}/link")]
[MapGroup<PlayersGroup>]
[Authorize(Policy = Permissions.ManagePlayers)]
public static partial class LinkPlayer
{
    public sealed record Command(Guid ProfileId, [property: FromQuery] string AccountName);

    private static async ValueTask<Results<Ok, NotFound, BadRequest>> HandleAsync(
        Command command,
        EarthDbContext earthDb,
        ApplicationDbContext webPortalDb,
        CancellationToken cancellationToken
    )
    {
        var account = await webPortalDb.Users
            .AsNoTracking()
            .Where(user => user.UserName == command.AccountName)
            .Select(user => new { user.Id, })
            .FirstOrDefaultAsync(cancellationToken);

        if (account is null)
        {
            return TypedResults.NotFound();
        }

        var rowsAffected = await earthDb.Profiles
            .Where(profile => profile.Id == command.ProfileId)
            .ExecuteUpdateAsync(profile => profile.SetProperty(profile => profile.WebPortalAccountId, account.Id), cancellationToken);

        return rowsAffected is 0 ? TypedResults.BadRequest() : TypedResults.Ok();
    }
}
