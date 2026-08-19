using Immediate.Apis.Shared;
using Immediate.Handlers.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Solace.Db.Playfab;
using Solace.Db.Playfab.Models.Items;
using Solace.WebPortal.Common;
using Solace.WebPortal.Common.Features.Store;

namespace Solace.WebPortal.Features.Store.Items;

[Handler]
[MapGet("items/summary")]
[MapGroup<StoreGroup>]
[Authorize(Policy = Permissions.ViewStore)]
public static partial class GetSummaryItems
{
    public sealed record Query;

    private static async ValueTask<List<ItemSummaryDto>> HandleAsync(
        Query _,
        PlayfabDbContext playfabDb,
        CancellationToken cancellationToken
    )
    {
        var result = new List<ItemSummaryDto>();

        await foreach (var item in playfabDb.Items
            .AsNoTracking()
            .Include(item => item.Data)
            .Select(item => new { item.Id, item.Title, item.Purchasable, item.StartDate, item.Data, })
            .AsAsyncEnumerable()
            .WithCancellation(cancellationToken))
        {
            result.Add(new ItemSummaryDto(item.Id, item.Title, item.Purchasable, item.StartDate, item.Data is BuildplateDataEF ? ItemDataTypeDto.Buildplate : ItemDataTypeDto.InventoryItem));
        }

        return result;
    }
}
