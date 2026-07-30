using Microsoft.EntityFrameworkCore;
using Solace.Db.Earth;
using Solace.Db.Earth.Models.Player;

namespace Solace.ApiServer.Utils;

internal static class JournalUtils
{
    public static async Task<int> AddCollectedItemAsync(EarthDbContext earthDb, ResultsEF.Builder results, Guid accountId, Guid itemId, DateTimeOffset timestamp, int count, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        
        var entry = await earthDb.JournalEntries
            .FirstOrDefaultAsync(e => e.ProfileId == accountId && e.ItemId == itemId, cancellationToken);

        int previousAmount;

        if (entry is null)
        {
            previousAmount = 0;

            entry = new ItemJournalEntryEF
            {
                ProfileId = accountId,
                ItemId = itemId,
                FirstSeen = timestamp,
                LastSeen = timestamp,
                AmountCollected = count
            };

            earthDb.JournalEntries.Add(entry);
        }
        else
        {
            previousAmount = entry.AmountCollected;

            entry.LastSeen = timestamp;
            entry.AmountCollected += count;
        }

        await earthDb.SaveChangesAsync(cancellationToken);

        results.Journal();

        return previousAmount;
    }
}