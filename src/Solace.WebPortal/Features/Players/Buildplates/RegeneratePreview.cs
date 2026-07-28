using Immediate.Apis.Shared;
using Immediate.Handlers.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Solace.BuildplateImporter;
using Solace.Db.Earth;
using Solace.EventBus.Client;
using Solace.ObjectStore.Client;
using Solace.WebPortal.Common;

namespace Solace.WebPortal.Features.Players.Buildplates;

[Handler]
[MapPost("{buildplateId}/regenerate-in-game-preview")]
[MapGroup<BuildplatesGroup>]
[Authorize(Policy = Permissions.ManagePlayers)]
public sealed partial class RegeneratePreview(
    EarthDbContext earthDb,
    EventBusClient eventBus,
    ObjectStoreClient objectStore,
    ILogger<RegeneratePreview> logger
)
{
    public sealed record Command([property: FromRoute] Guid PlayerId, [property: FromRoute] Guid BuildplateId);

    private async ValueTask<Results<Ok, BadRequest<string>>> HandleAsync(
        Command command,
        CancellationToken cancellationToken)
    {
        await using var importer = new Importer(earthDb, eventBus, objectStore, logger)
        {
            OwnsEarthDb = true,
            OwnsEventBusClient = false,
            OwnsObjectStoreClient = false,
        };

        var result = await importer.RegeneratePlayerBuildplatePreviewAsync(command.PlayerId, command.BuildplateId, cancellationToken);
        return result ? TypedResults.Ok() : TypedResults.BadRequest("Failed to regenerate preview.");
    }
}
