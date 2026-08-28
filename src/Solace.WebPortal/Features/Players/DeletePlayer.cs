using Immediate.Apis.Shared;
using Immediate.Handlers.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Solace.Db.Earth;
using Solace.ObjectStore.Client;
using Solace.WebPortal.Common;

namespace Solace.WebPortal.Features.Players;

[Handler]
[MapDelete("{id}")]
[MapGroup<PlayersGroup>]
[Authorize(Policy = Permissions.ManagePlayers)]
public static partial class DeletePlayer
{
    public sealed record Command(Guid Id);

    private static async ValueTask<Results<Ok, NotFound>> HandleAsync(
        Command command,
        EarthDbContext earthDb,
        ObjectStoreClient objectStore,
        CancellationToken cancellationToken
    )
    {
        var buildplateObjects = await earthDb.PlayerBuildplates
           .AsNoTracking()
           .Where(bp => bp.ProfileId == command.Id)
           .Select(bp => new { bp.ServerDataObjectId, bp.PreviewObjectId, })
           .ToListAsync(cancellationToken);

        var sharedBuildplateObjects = await earthDb.SharedBuildplates
           .AsNoTracking()
           .Where(bp => bp.ProfileId == command.Id)
           .Select(bp => new { bp.ServerDataObjectId, })
           .ToListAsync(cancellationToken);

        var rowsDeleted = await earthDb.Profiles
            .Where(account => account.Id == command.Id)
            .ExecuteDeleteAsync(cancellationToken);

        if (rowsDeleted is 0)
        {
            return TypedResults.NotFound();
        }

        foreach (var bp in buildplateObjects)
        {
            await objectStore.DeleteAsync(bp.ServerDataObjectId, cancellationToken);

            await objectStore.DeleteAsync(bp.PreviewObjectId, cancellationToken);
        }

        foreach (var bp in sharedBuildplateObjects)
        {
            await objectStore.DeleteAsync(bp.ServerDataObjectId, cancellationToken);
        }

        return TypedResults.Ok();
    }
}
