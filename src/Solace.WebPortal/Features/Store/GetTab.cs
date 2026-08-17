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

namespace Solace.WebPortal.Features.Store;

[Handler]
[MapGet("tabs/{TabIndex}")]
[MapGroup<StoreGroup>]
[Authorize(Policy = Permissions.ViewStore)]
public static partial class GetTab
{
    public sealed record Query([property: FromRoute] int TabIndex);

    private static async ValueTask<Results<Ok<TabDto>, NotFound>> HandleAsync(
        Query query,
        PlayfabDbContext playfabDb,
        CancellationToken cancellationToken
    )
    {
        var tab = await playfabDb.Tabs
            .AsNoTracking()
            .FirstOrDefaultAsync(tab => tab.TabIndex == query.TabIndex, cancellationToken);

        if (tab is null)
        {
            return TypedResults.NotFound();
        }

        return TypedResults.Ok(new TabDto(
            tab.TabId,
            tab.TabTitle,
            tab.TabIcon,
            tab.ScreenLayoutQueries.Select(sq => new ScreenLayoutQueryDto(
                sq.ComponentId,
                sq.ColumnType switch
                {
                    ColumnTypeEF.Rectangle => ColumnTypeDto.Rectangle,
                    ColumnTypeEF.Square => ColumnTypeDto.Square,
                    ColumnTypeEF.Grid => ColumnTypeDto.Grid,
                    _ => throw new UnreachableException(),
                },
                sq.Queries.Select(q => new QueryDto(
                    q.TopCount,
                    q.QueryContentTypes.Select(type => type switch
                    {
                        ContentTypeEF.Durable => QueryContentTypeDto.Durable,
                        ContentTypeEF.Collection => QueryContentTypeDto.Collection,
                        ContentTypeEF.Bundle => QueryContentTypeDto.Bundle,
                        ContentTypeEF.Persona => QueryContentTypeDto.Persona,
                        ContentTypeEF.Genoa => QueryContentTypeDto.Genoa,
                        ContentTypeEF.BuildplateOffer => QueryContentTypeDto.BuildplateOffer,
                        ContentTypeEF.RubyOffer => QueryContentTypeDto.RubyOffer,
                        ContentTypeEF.InventoryItemOffer => QueryContentTypeDto.InventoryItemOffer,
                        _ => throw new UnreachableException(),
                    }),
                    q.ProductIds
                ))
            ))
        ));
    }
}
