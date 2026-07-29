using Immediate.Apis.Shared;
using Immediate.Handlers.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Solace.BuildplateImporter;
using Solace.Db.Earth;
using Solace.ObjectStore.Client;
using Solace.WebPortal.Common;
using Solace.WebPortal.Data;

namespace Solace.WebPortal.Features.Players.Buildplates;

[Handler]
[MapDelete("{buildplateId}")]
[MapGroup<BuildplatesGroup>]
[Authorize(Policy = Permissions.ManagePlayers)]
public sealed partial class DeleteBuildplate(
    EarthDbContext earthDb,
    ApplicationDbContext appDb,
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

        var success = await importer.RemoveBuildplateFromPlayer(command.BuildplateId, command.PlayerId, cancellationToken);
        if (success)
        {
            await appDb.BuildplatePreviews
                .Where(buildplate => buildplate.BuildplateId == command.BuildplateId && buildplate.PlayerId == command.PlayerId)
                .ExecuteDeleteAsync(cancellationToken);
        }

        return success ? TypedResults.Ok() : TypedResults.BadRequest();
    }
}
