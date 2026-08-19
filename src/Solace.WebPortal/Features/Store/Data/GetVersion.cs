using Immediate.Apis.Shared;
using Immediate.Handlers.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Solace.Db.Playfab;
using Solace.StaticData;
using Solace.WebPortal.Common;
using Solace.WebPortal.Common.Features.Store;

namespace Solace.WebPortal.Features.Store.Data;

[Handler]
[MapGet("data/version")]
[MapGroup<StoreGroup>]
[Authorize(Policy = Permissions.ViewStore)]
public static partial class GetVersion
{
    public sealed record Query();

    private static async ValueTask<StoreVersionInfo> HandleAsync(
        Query _,
        PlayfabDbContext playfabDb,
        StaticDataProvider staticData,
        CancellationToken cancellationToken
    )
    {
        var seedHistory = await playfabDb.SeedingHistory.FirstOrDefaultAsync(history => history.Key == "PlayfabData", cancellationToken);

        return new StoreVersionInfo(seedHistory?.Version, staticData.Playfab.Version);
    }
}

