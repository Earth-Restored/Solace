using Immediate.Apis.Shared;
using Immediate.Handlers.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Solace.Db.Earth;
using Solace.Db.Playfab;
using Solace.Db.Playfab.Models.Items;
using Solace.StaticData;
using Solace.WebPortal.Common;
using Solace.WebPortal.Common.Features.Store;

namespace Solace.WebPortal.Features.Store;

[Handler]
[MapGet("items/{ItemId}")]
[MapGroup<StoreGroup>]
[Authorize(Policy = Permissions.ViewStore)]
public static partial class GetItem
{
    public sealed record Query([property: FromRoute] Guid ItemId);

    private static async ValueTask<Results<Ok<ItemDto>, NotFound>> HandleAsync(
        Query query,
        PlayfabDbContext playfabDb,
        EarthDbContext earthDb,
        StaticDataProvider staticData,
        CancellationToken cancellationToken
    )
    {
        var item = await playfabDb.Items
            .AsNoTracking()
            .Include(item => item.Data)
            .FirstOrDefaultAsync(item => item.Id == query.ItemId, cancellationToken);

        if (item is null or { Data: not (BuildplateDataEF or InventoryItemDataEF) })
        {
            return TypedResults.NotFound();
        }

        return TypedResults.Ok(await ItemDtoUtils.MapItemAsync(item, earthDb, staticData.Catalog.ItemsCatalog, cancellationToken));
    }
}
