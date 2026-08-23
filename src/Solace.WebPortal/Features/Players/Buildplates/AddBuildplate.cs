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
using Solace.WebPortal.Common.Features.Players.Buildplates;

namespace Solace.WebPortal.Features.Players.Buildplates;

[Handler]
[MapPost("")]
[MapGroup<BuildplatesGroup>]
[Authorize(Policy = Permissions.ManagePlayers)]
public sealed partial class AddBuildplate(
    EarthDbContext earthDb,
    EventBusClient eventBus,
    ObjectStoreClient objectStore,
    ILogger<AddBuildplate> logger
)
{
    public sealed record Command([property: FromRoute] Guid PlayerId, [property: FromBody] AddBuildplateCommand Body);

    private async ValueTask<Results<Ok<AddBuildplateResponse>, BadRequest>> HandleAsync(
        Command command,
        CancellationToken cancellationToken
    )
    {
        await using var importer = new Importer(earthDb, eventBus, objectStore, logger)
        {
            OwnsEarthDb = true,
            OwnsEventBusClient = false,
            OwnsObjectStoreClient = false,
        };

        var buildplate = await importer.AddBuidplateToPlayer(command.Body.TemplateId, command.PlayerId, cancellationToken);
        if (buildplate is null)
        {
            return TypedResults.BadRequest();
        }

        return TypedResults.Ok(new AddBuildplateResponse(
            buildplate.Id
        ));
    }
}
