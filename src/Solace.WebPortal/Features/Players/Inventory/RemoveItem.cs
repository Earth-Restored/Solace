using Immediate.Apis.Shared;
using Immediate.Handlers.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Solace.Db.Earth;
using Solace.StaticData;
using Solace.WebPortal.Common;

namespace Solace.WebPortal.Features.Players.Inventory;

[Handler]
[MapDelete("{itemId}")]
[MapGroup<InventoryGroup>]
[Authorize(Policy = Permissions.ManagePlayers)]
public static partial class RemoveItem
{
    public sealed record Command([property: FromRoute] Guid Id, [property: FromRoute] Guid ItemId, [property: FromQuery] Guid? InstanceId);

    private static async ValueTask<Results<Ok, NotFound, BadRequest>> HandleAsync(
        Command command,
        EarthDbContext earthDb,
        StaticDataProvider staticData,
        CancellationToken cancellationToken
    )
    {
        if (!staticData.Catalog.ItemsCatalog.TryGetItem(command.ItemId, out var item))
        {
            return TypedResults.BadRequest();
        }

        bool removed;
        if (item.Stackable)
        {
            removed = await earthDb.StackableItems
                .Where(item => item.ProfileId == command.Id && item.ItemId == command.ItemId)
                .ExecuteDeleteAsync(cancellationToken) > 0;
        }
        else
        {
            if (command.InstanceId is not { } instanceId)
            {
                return TypedResults.BadRequest();
            }

            removed = await earthDb.NonStackableItems
                .Where(item => item.ProfileId == command.Id && item.ItemId == command.ItemId && item.InstanceId == instanceId)
                .ExecuteDeleteAsync(cancellationToken) > 0;
        }

        return removed ? TypedResults.Ok() : TypedResults.NotFound();
    }
}
