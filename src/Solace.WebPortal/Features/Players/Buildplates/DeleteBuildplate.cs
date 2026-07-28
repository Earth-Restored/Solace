using Immediate.Apis.Shared;
using Immediate.Handlers.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Solace.BuildplateImporter;
using Solace.Db.Earth;
using Solace.ObjectStore.Client;
using Solace.WebPortal.Common;

namespace Solace.WebPortal.Features.Players.Buildplates;

[Handler]
[MapDelete("{buildplateId}")]
[MapGroup<BuildplatesGroup>]
[Authorize(Policy = Permissions.ManagePlayers)]
public sealed partial class DeleteBuildplate(
    EarthDbContext earthDb,
    ObjectStoreClient objectStore,
    ILogger<DeleteBuildplate> logger
)
{
    public sealed record Command([property: FromRoute] Guid PlayerId, [property: FromRoute] Guid BuildplateId);

    private async ValueTask<Results<Ok, BadRequest>> HandleAsync(
        Command command,
        CancellationToken cancellationToken
    )
    {
        await using var importer = new Importer(earthDb, null, objectStore, logger)
        {
            OwnsEarthDb = true,
            OwnsEventBusClient = false,
            OwnsObjectStoreClient = false,
        };

        return await importer.RemoveBuildplateFromPlayer(command.BuildplateId, command.PlayerId, cancellationToken) ? TypedResults.Ok() : TypedResults.BadRequest();
    }
}
