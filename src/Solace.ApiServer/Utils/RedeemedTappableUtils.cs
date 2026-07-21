using Microsoft.EntityFrameworkCore;
using Solace.DB.Earth;

namespace Solace.ApiServer.Utils;

internal static  class RedeemedTappableUtils
{
    public static async Task PruneAsync(EarthDbContext earthDb, DateTimeOffset currentTime, CancellationToken cancellationToken = default)
        => await earthDb.RedeemedTappables
            .Where(rt => rt.ExpiresAt < currentTime)
            .ExecuteDeleteAsync(cancellationToken);
}
