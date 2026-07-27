using Immediate.Apis.Shared;
using Immediate.Handlers.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Solace.Db.Earth;
using Solace.Db.Earth.Models.Player;
using Solace.StaticData;
using Solace.WebPortal.Common;
using Solace.WebPortal.Common.Features.Players.Inventory;

namespace Solace.WebPortal.Features.Players.Inventory;

[Handler]
[MapPost("")]
[MapGroup<InventoryGroup>]
[Authorize(Policy = Permissions.ManagePlayers)]
public static partial class AddItem
{
    public sealed record Command([property: FromRoute] Guid Id, [property: FromBody] AddItemCommand Body);

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
            var existingItem = await earthDb.StackableItems
                .FirstOrDefaultAsync(item => item.AccountId == command.Id && item.ItemId == command.Body.ItemId, cancellationToken);

            if (existingItem is not null)
            {
                existingItem.Count += command.Body.Count;
            }
            else
            {
                earthDb.StackableItems.Add(new StackableItemEF(command.Id, command.Body.ItemId, command.Body.Count));
            }
        }
        else
        {
            for (var i = 0; i < command.Body.Count; i++)
            {
                earthDb.NonStackableItems.Add(new NonStackableItemInstanceEF(command.Id, command.Body.ItemId, Guid.NewGuid(), 0));
            }
        }

        await earthDb.SaveChangesAsync(cancellationToken);

        return TypedResults.Ok();
    }
}
