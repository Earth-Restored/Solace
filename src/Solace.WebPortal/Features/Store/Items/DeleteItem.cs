using Immediate.Apis.Shared;
using Immediate.Handlers.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Solace.Db.Playfab;
using Solace.EventBus.Client;
using Solace.WebPortal.Common;

namespace Solace.WebPortal.Features.Store.Items;

[Handler]
[MapDelete("items/{ItemId}")]
[MapGroup<StoreGroup>]
[Authorize(Policy = Permissions.EditRoles)]
public static partial class DeleteItem
{
    public sealed record Command([property: FromRoute] Guid ItemId);

    private static async ValueTask<Results<Ok, NotFound>> HandleAsync(
        Command command,
        PlayfabDbContext playfabDb,
        EventBusClient eventBus,
        CancellationToken cancellationToken
    )
    {
        var rowsAffected = await playfabDb.Items
            .Where(item => item.Id == command.ItemId)
            .ExecuteDeleteAsync(cancellationToken);

        if (rowsAffected is 0)
        {
            return TypedResults.NotFound();
        }

        await eventBus.PublishAsync("playfab", "shop_data_updated", "", cancellationToken);

        return TypedResults.Ok();
    }
}
