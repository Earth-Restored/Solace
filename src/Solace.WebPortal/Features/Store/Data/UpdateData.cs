using Immediate.Apis.Shared;
using Immediate.Handlers.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Solace.AuthServer.Utils;
using Solace.Db.Playfab;
using Solace.EventBus.Client;
using Solace.StaticData;
using Solace.WebPortal.Common;

namespace Solace.WebPortal.Features.Store.Data;

[Handler]
[MapPost("data/update")]
[MapGroup<StoreGroup>]
[Authorize(Policy = Permissions.EditStore)]
public static partial class UpdateData
{
    public sealed record Command;

    private static async ValueTask<Ok> HandleAsync(
        Command? _,
        PlayfabDbContext playfabDb,
        EventBusClient eventBus,
        StaticDataProvider staticData,
        CancellationToken cancellationToken
    )
    {
        await DataSeedUtils.SeedPlayfabDataAsync(playfabDb, staticData.Playfab, update: true, force: false, cancellationToken);

        await eventBus.PublishAsync("playfab", "shop_data_updated", "", cancellationToken);

        return TypedResults.Ok();
    }
}

