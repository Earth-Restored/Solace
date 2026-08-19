using Immediate.Apis.Shared;
using Immediate.Handlers.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Solace.Db.Earth;
using Solace.Db.Playfab;
using Solace.Db.Playfab.Models.Items;
using Solace.StaticData;
using Solace.WebPortal.Common;
using Solace.WebPortal.Common.Features.Store;

namespace Solace.WebPortal.Features.Store.Items;

[Handler]
[MapGet("items")]
[MapGroup<StoreGroup>]
[Authorize(Policy = Permissions.ViewStore)]
public static partial class GetItems
{
    public sealed record Query;

    private static async ValueTask<List<ItemDto>> HandleAsync(
        Query _,
        PlayfabDbContext playfabDb,
        EarthDbContext earthDb,
        StaticDataProvider staticData,
        CancellationToken cancellationToken
    )
    {
        var result = new List<ItemDto>();

        await foreach (var item in playfabDb.Items
            .AsNoTracking()
            .Include(item => item.Data)
            .AsAsyncEnumerable()
            .WithCancellation(cancellationToken))
        {
            if (item.Data is not (BuildplateDataEF or InventoryItemDataEF))
            {
                continue;
            }

            result.Add(await ItemDtoUtils.MapItemAsync(item, earthDb, staticData.Catalog.ItemsCatalog, cancellationToken));
        }

        return result;
    }
}
