using System.Diagnostics;
using Immediate.Apis.Shared;
using Immediate.Handlers.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Solace.Db.Playfab;
using Solace.Db.Playfab.Models.Tabs;
using Solace.WebPortal.Common;
using Solace.WebPortal.Common.Features.Store;

namespace Solace.WebPortal.Features.Store.Tabs;

[Handler]
[MapPut("tabs/{TabIndex}")]
[MapGroup<StoreGroup>]
[Authorize(Policy = Permissions.EditStore)]
public static partial class UpdateTab
{
    public sealed record Command([property: FromRoute] int TabIndex, [property: FromBody] TabDto Tab);

    private static async ValueTask<Results<Ok, NotFound, BadRequest>> HandleAsync(
        Command command,
        PlayfabDbContext playfabDb,
        CancellationToken cancellationToken
    )
    {
        if (!TabDtoUtils.IsValid(command.Tab))
        {
            return TypedResults.BadRequest();
        }

        var tab = await playfabDb.Tabs
            .AsTracking()
            .FirstOrDefaultAsync(tab => tab.TabIndex == command.TabIndex, cancellationToken);

        if (tab is null)
        {
            return TypedResults.NotFound();
        }

        tab.TabId = command.Tab.TabId.Trim();
        tab.TabTitle = command.Tab.TabTitle.Trim();
        tab.TabIcon = command.Tab.TabIcon.Trim();
        tab.ScreenLayoutQueries.Clear();
        tab.ScreenLayoutQueries.AddRange(command.Tab.ScreenLayoutQueries.Select(static sq => new ScreenLayoutQueryEF()
        {
            Id = Guid.CreateVersion7(),
            ColumnType = sq.ColumnType switch
            {
                ColumnTypeDto.Rectangle => ColumnTypeEF.Rectangle,
                ColumnTypeDto.Square => ColumnTypeEF.Square,
                ColumnTypeDto.Grid => ColumnTypeEF.Grid,
                _ => throw new UnreachableException(),
            },
            ComponentId = Guid.NewGuid(),
            Queries = [.. sq.Queries.Select(static q => new QueryEF()
            {
                Id = Guid.NewGuid(),
                TopCount = int.Max(25, q.ProductIds.Count()),
                ProductIds = [.. q.ProductIds],
                QueryContentTypes = [ContentTypeEF.Durable, ContentTypeEF.Collection, ContentTypeEF.Bundle, ContentTypeEF.Persona, ContentTypeEF.Genoa, ContentTypeEF.BuildplateOffer, ContentTypeEF.RubyOffer, ContentTypeEF.InventoryItemOffer ],
            })],
        }));

        await playfabDb.SaveChangesAsync(cancellationToken);

        return TypedResults.Ok();
    }
}
