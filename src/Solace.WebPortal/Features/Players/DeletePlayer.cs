using Immediate.Apis.Shared;
using Immediate.Handlers.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Solace.Common.Utils;
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
        await using var transaction = await earthDb.Database.BeginTransactionAsync(cancellationToken);

        var result = await ProfileDeleteUtil.DeleteProfile(command.Id, earthDb, objectStore, cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        return result
            ? TypedResults.Ok()
            : TypedResults.NotFound();
    }
}
