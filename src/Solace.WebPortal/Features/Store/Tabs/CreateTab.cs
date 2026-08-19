using System.Diagnostics;
using Immediate.Apis.Shared;
using Immediate.Handlers.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Solace.Db.Playfab;
using Solace.Db.Playfab.Models.Tabs;
using Solace.EventBus.Client;
using Solace.WebPortal.Common;
using Solace.WebPortal.Common.Features.Store;

namespace Solace.WebPortal.Features.Store.Tabs;

[Handler]
[MapPost("tabs")]
[MapGroup<StoreGroup>]
[Authorize(Policy = Permissions.EditStore)]
public static partial class CreateTab
{
    public sealed record Command([property: FromBody] TabDto Tab);

    private static async ValueTask<Results<Ok<int>, BadRequest>> HandleAsync(
        Command command,
        PlayfabDbContext playfabDb,
        EventBusClient eventBus,
        CancellationToken cancellationToken
    )
    {
        if (!TabDtoUtils.IsValid(command.Tab))
        {
            return TypedResults.BadRequest();
        }

        await using var transaction = await playfabDb.Database.BeginTransactionAsync(cancellationToken);

        var maxIndex = await playfabDb.Tabs
            .AsNoTracking()
            .MaxAsync(tab => (int?)tab.TabIndex, cancellationToken);

        Debug.Assert(maxIndex is null or >= 1);

        maxIndex ??= 0;

        var tab = new TabEF()
        {
            TabIndex = maxIndex.Value + 1,
            TabId = command.Tab.TabId.Trim(),
            TabTitle = command.Tab.TabTitle.Trim(),
            TabIcon = command.Tab.TabIcon.Trim(),
            ScreenLayoutQueries = [.. command.Tab.ScreenLayoutQueries.Select(static sq => new ScreenLayoutQueryEF()
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
            })],
        };

        playfabDb.Tabs.Add(tab);
        await playfabDb.SaveChangesAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        await eventBus.PublishAsync("playfab", "shop_data_updated", "", cancellationToken);

        return TypedResults.Ok(tab.TabIndex);
    }
}
