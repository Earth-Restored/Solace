using Immediate.Apis.Shared;
using Immediate.Handlers.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Solace.Db.Playfab;
using Solace.EventBus.Client;
using Solace.WebPortal.Common;

namespace Solace.WebPortal.Features.Store.Tabs;

[Handler]
[MapDelete("tabs/{TabIndex}")]
[MapGroup<StoreGroup>]
[Authorize(Policy = Permissions.EditStore)]
public static partial class DeleteTab
{
    public sealed record Command([property: FromRoute] int TabIndex);

    private static async ValueTask<Results<Ok, NotFound, BadRequest>> HandleAsync(
        Command command,
        PlayfabDbContext playfabDb,
        EventBusClient eventBus,
        CancellationToken cancellationToken
    )
    {
        var rowsAffected = await playfabDb.Tabs
            .Where(item => item.TabIndex == command.TabIndex)
            .ExecuteDeleteAsync(cancellationToken);

        if (rowsAffected is 0)
        {
            return TypedResults.NotFound();
        }

        await eventBus.PublishAsync("playfab", "shop_data_updated", "", cancellationToken);

        return TypedResults.Ok();
    }
}
