using Immediate.Apis.Shared;
using Immediate.Handlers.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Solace.Db.Earth;
using Solace.ObjectStore.Client;
using Solace.WebPortal.Common;
using Solace.WebPortal.Data;

namespace Solace.WebPortal.Features.Data;

[Handler]
[MapPost("delete-all")]
[MapGroup<DataGroup>]
[Authorize(Policy = Permissions.EditData)]
public static partial class DeleteAll
{
    public sealed record Query;

    private static async ValueTask<Ok> HandleAsync(
        Query _,
        EarthDbContext earthDb,
        ApplicationDbContext webPortalDb,
        ObjectStoreClient objectStore,
        CancellationToken cancellationToken
    )
    {
#pragma warning disable CS0618 // Type or member is obsolete - should only be called from the DeleteAll endpoint
        await earthDb.DeleteAllAsync(cancellationToken);
        await webPortalDb.DeleteAllAsync(cancellationToken);
        await objectStore.DeleteAllAsync(cancellationToken);
#pragma warning restore CS0618 // Type or member is obsolete

        return TypedResults.Ok();
    }
}
