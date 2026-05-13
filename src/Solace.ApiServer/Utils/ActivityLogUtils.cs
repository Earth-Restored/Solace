using Microsoft.EntityFrameworkCore;
using Solace.DB;
using Solace.DB.Models.Player;
using Solace.DB.Utils;

namespace Solace.ApiServer.Utils;

public static class ActivityLogUtils
{
    public static async Task AddEntryAsync(EarthDbContext.Results results, Guid accountId, ActivityLogEF.Entry entry)
    {
        var activityLog = await results.EarthDb.ActivityLogs
            .AsTracking()
            .FirstOrNewAsync(activityLog => activityLog.Id == accountId);

        activityLog.AddEntry(entry);
        activityLog.Prune();

        await results.EarthDb.SaveChangesAsync();
    }
}
