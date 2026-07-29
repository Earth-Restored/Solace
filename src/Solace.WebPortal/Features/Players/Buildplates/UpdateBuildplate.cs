using Immediate.Apis.Shared;
using Immediate.Handlers.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Solace.Db.Earth;
using Solace.WebPortal.Common;
using Solace.WebPortal.Common.Features.Players.Buildplates;

namespace Solace.WebPortal.Features.Players.Buildplates;

[Handler]
[MapPut("{buildplateId}")]
[MapGroup<BuildplatesGroup>]
[Authorize(Policy = Permissions.ManagePlayers)]
public static partial class UpdateBuildplate
{
    public sealed record Command(
        [property: FromRoute] Guid PlayerId,
        [property: FromRoute] Guid BuildplateId,
        [property: FromBody] UpdateBuildplateCommand Body
    );

    private static async ValueTask<Results<Ok, NotFound, BadRequest<string>>> HandleAsync(
        Command command,
        EarthDbContext earthDb,
        CancellationToken cancellationToken
    )
    {
        var dbBuildplate = await earthDb.PlayerBuildplates
            .AsTracking()
            .FirstOrDefaultAsync(buildplate => buildplate.Id == command.BuildplateId && buildplate.ProfileId == command.PlayerId, cancellationToken);

        if (dbBuildplate is null)
        {
            return TypedResults.NotFound();
        }

        if (command.Body.Name is { } name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return TypedResults.BadRequest("Name cannot be empty.");
            }

            if (name.Length > 80)
            {
                return TypedResults.BadRequest("Name is too long.");
            }

            dbBuildplate.Name = name;
        }

        if (command.Body.BlocksPerMeter is { } blocksPerMeter)
        {
            if (blocksPerMeter is < 1 or > 100)
            {
                return TypedResults.BadRequest("BlocksPerMeter must be between 1 and 100.");
            }

            dbBuildplate.BlocksPerMeter = blocksPerMeter;
        }

        await earthDb.SaveChangesAsync(cancellationToken);

        return TypedResults.Ok();
    }
}
