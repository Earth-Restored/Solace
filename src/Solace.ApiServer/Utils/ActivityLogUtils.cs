using Microsoft.EntityFrameworkCore;
using Solace.Db.Earth;
using Solace.Db.Earth.Models.Player;

namespace Solace.ApiServer.Utils;

internal static class ActivityLogUtils
{
    public static async Task AddEntryAsync(EarthDbContext earthDb, ResultsEF.Builder results, Guid accountId, ActivityLogEntryEF entry, CancellationToken cancellationToken = default)
    {
        _ = results;

        earthDb.ActivityLogs.Add(entry);

        await earthDb.SaveChangesAsync(cancellationToken);

        var thresholdTimestamp = await earthDb.ActivityLogs
            .Where(log => log.ProfileId == accountId)
            .OrderByDescending(log => log.Timestamp)
            .Select(log => log.Timestamp)
            .Skip(39) // Skip the first 39 (0-indexed)
            .FirstOrDefaultAsync(cancellationToken);

        if (thresholdTimestamp != default)
        {
            await earthDb.ActivityLogs
                .Where(log => log.ProfileId == accountId && log.Timestamp < thresholdTimestamp)
                .ExecuteDeleteAsync(cancellationToken);
        }
    }
}
