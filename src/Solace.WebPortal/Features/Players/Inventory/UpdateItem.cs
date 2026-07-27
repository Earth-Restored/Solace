using Immediate.Apis.Shared;
using Immediate.Handlers.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Solace.Db.Earth;
using Solace.StaticData;
using Solace.WebPortal.Common;
using Solace.WebPortal.Common.Features.Players.Inventory;

namespace Solace.WebPortal.Features.Players.Inventory;

[Handler]
[MapPut("")]
[MapGroup<InventoryGroup>]
[Authorize(Policy = Permissions.ManagePlayers)]
public static partial class UpdateItem
{
    public sealed record Command([property: FromRoute] Guid Id, [property: FromBody] UpdateItemCommand Body);

    private static async ValueTask<Results<Ok, BadRequest>> HandleAsync(
        Command command,
        EarthDbContext earthDb,
        StaticDataProvider staticData,
        CancellationToken cancellationToken
    )
    {
        if (!staticData.Catalog.ItemsCatalog.TryGetItem(command.Body.ItemId, out var item))
        {
            return TypedResults.BadRequest();
        }

        if (item.Stackable)
        {
            if (command.Body.Count is not { } count)
            {
                return TypedResults.Ok();
            }

            if (count < 0)
            {
                return TypedResults.BadRequest();
            }

            var dbItem = await earthDb.StackableItems
                .FirstOrDefaultAsync(item => item.AccountId == command.Id && item.ItemId == command.Body.ItemId, cancellationToken);

            if (dbItem is null)
            {
                return TypedResults.BadRequest();
            }

            dbItem.Count = count;
        }
        else
        {
            if (command.Body.Wear is not { } wear)
            {
                return TypedResults.Ok();
            }

            if (wear < 0 || (item.ToolInfo is not null && wear > item.ToolInfo.MaxWear))
            {
                return TypedResults.BadRequest();
            }

            var dbItem = await earthDb.NonStackableItems
                .FirstOrDefaultAsync(item => item.AccountId == command.Id && item.ItemId == command.Body.ItemId && item.InstanceId == command.Body.InstanceId, cancellationToken);

            if (dbItem is null)
            {
                return TypedResults.BadRequest();
            }

            dbItem.Wear = wear;
        }

        await earthDb.SaveChangesAsync(cancellationToken);

        return TypedResults.Ok();
    }
}
