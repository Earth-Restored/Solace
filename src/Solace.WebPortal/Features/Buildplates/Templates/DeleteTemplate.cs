using Immediate.Apis.Shared;
using Immediate.Handlers.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Solace.BuildplateImporter;
using Solace.Db.Earth;
using Solace.ObjectStore.Client;
using Solace.WebPortal.Common;
using Solace.WebPortal.Data;

namespace Solace.WebPortal.Features.Buildplates.Templates;

[Handler]
[MapDelete("{id}")]
[MapGroup<TemplatesGroup>]
[Authorize(Policy = Permissions.ManageBuildplates)]
public sealed partial class DeleteTemplate(
    EarthDbContext earthDb,
    ApplicationDbContext appDb,
    ObjectStoreClient objectStore,
    ILogger<DeleteTemplate> logger
)
{
    public sealed record Command(Guid Id, bool RemoveTemplateFromPlayers);

    private async ValueTask<Results<NoContent, BadRequest<string>>> HandleAsync(
        Command command,
        CancellationToken cancellationToken)
    {
        await using var importer = new Importer(earthDb, null, objectStore, logger)
        {
            OwnsEarthDb = true,
            OwnsEventBusClient = false,
            OwnsObjectStoreClient = false,
        };

        var success = await importer.RemoveTemplateAsync(command.Id, command.RemoveTemplateFromPlayers, cancellationToken);
        if (success)
        {
            await appDb.BuildplatePreviews
                .Where(buildplate => buildplate.BuildplateId == command.Id && buildplate.PlayerId == Guid.Empty)
                .ExecuteDeleteAsync(cancellationToken);
        }

        return success ? TypedResults.NoContent() : TypedResults.BadRequest("Failed to delete buildplate.");
    }
}