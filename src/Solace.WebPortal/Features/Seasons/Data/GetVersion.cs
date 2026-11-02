using Immediate.Apis.Shared;
using Immediate.Handlers.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Solace.Db.Earth;
using Solace.StaticData;
using Solace.WebPortal.Common;
using Solace.WebPortal.Common.Features.Seasons;

namespace Solace.WebPortal.Features.Seasons.Data;

[Handler]
[MapGet("data/version")]
[MapGroup<SeasonsGroup>]
[Authorize(Policy = Permissions.ViewSeasonsAndChallenges)]
public static partial class GetVersion
{
    public sealed record Query;

    private static async ValueTask<SeasonsVersionInfo> HandleAsync(
        Query _,
        EarthDbContext earthDb,
        StaticDataProvider staticData,
        CancellationToken cancellationToken
    )
    {
        var seedHistory = await earthDb.SeedingHistory
            .AsNoTracking()
            .FirstOrDefaultAsync(history => history.Key == "SeasonsData", cancellationToken);

        return new SeasonsVersionInfo(
            seedHistory?.Version,
            staticData.Seasons.Version
        );
    }
}
