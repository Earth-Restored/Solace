using Immediate.Apis.Shared;
using Immediate.Handlers.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Solace.Db.Playfab;
using Solace.WebPortal.Common;
using Solace.WebPortal.Common.Features.Store;

namespace Solace.WebPortal.Features.Store.Tabs;

[Handler]
[MapGet("tabs/summary")]
[MapGroup<StoreGroup>]
[Authorize(Policy = Permissions.ViewStore)]
public static partial class GetSummaryTabs
{
    public sealed record Query([property: FromQuery] bool IncludeItems);

    private static async ValueTask<List<TabSummaryDto>> HandleAsync(
        Query query,
        PlayfabDbContext playfabDb,
        CancellationToken cancellationToken
    )
    {
        var result = new List<TabSummaryDto>();

        await foreach (var tab in playfabDb.Tabs
            .AsNoTracking()
            .OrderBy(tab => tab.TabIndex)
            .Select(tab => new { tab.TabId, tab.ScreenLayoutQueries })
            .AsAsyncEnumerable()
            .WithCancellation(cancellationToken))
        {
            result.Add(new TabSummaryDto(tab.TabId, query.IncludeItems ? [.. tab.ScreenLayoutQueries.SelectMany(sq => sq.Queries).SelectMany(q => q.ProductIds)] : null));
        }

        return result;
    }
}
