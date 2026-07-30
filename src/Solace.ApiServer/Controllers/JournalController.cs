using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Solace.ApiServer.Utils;
using Solace.Common;
using Solace.Db.Earth;
using Solace.Db.Earth.Models.Player;
using Microsoft.EntityFrameworkCore;
using Solace.ApiServer.Types.Journal;

namespace Solace.ApiServer.Controllers;

[Authorize]
[ApiVersion("1.1")]
[Route("1/api/v{version:apiVersion}/player/journal")]
internal sealed class JournalController : SolaceControllerBase
{
    private readonly EarthDbContext _earthDb;

    public JournalController(EarthDbContext earthDB)
    {
        _earthDb = earthDB;
    }

    [HttpGet]
    public async Task<Results<ContentHttpResult, BadRequest>> Get(CancellationToken cancellationToken)
    {
        if (!TryGetAccountId(out var accountId))
        {
            return TypedResults.BadRequest();
        }

        var journalEnties = _earthDb.JournalEntries
            .AsNoTracking()
            .Where(entry => entry.ProfileId == accountId)
            .Select(entry => new { entry.ItemId, entry.FirstSeen, entry.LastSeen, entry.AmountCollected, })
            .AsAsyncEnumerable()
            .WithCancellation(cancellationToken);

        var activityLogEntries = _earthDb.ActivityLogs
            .AsNoTracking()
            .Where(entry => entry.ProfileId == accountId)
            .AsAsyncEnumerable();

        Dictionary<Guid, Types.Journal.JournalRecord.InventoryJournalEntry> inventoryJournal = [];
        await foreach (var itemJournalEntry in journalEnties)
        {
            inventoryJournal[itemJournalEntry.ItemId] = new Types.Journal.JournalRecord.InventoryJournalEntry(
                TimeFormatter.FormatTime(itemJournalEntry.FirstSeen),
                TimeFormatter.FormatTime(itemJournalEntry.LastSeen),
                itemJournalEntry.AmountCollected
            );
        }

        var activityLog = await activityLogEntries.Select(ActivityLogEntryToApiResponse).ToArrayAsync(cancellationToken);
        Array.Reverse(activityLog);

        var resp = Json.Serialize(new EarthApiResponse(new Types.Journal.JournalRecord(inventoryJournal, activityLog)));
        return TypedResults.Content(resp, "application/json");
    }

    private static Types.Journal.JournalRecord.ActivityLogEntry ActivityLogEntryToApiResponse(ActivityLogEntryEF entry)
    {
        Rewards rewards = entry switch
        {
            LevelUpEntryEF levelUp => new Rewards().SetLevel(levelUp.Level),
            TappableEntryEF tappable => Rewards.FromDBRewardsModel(tappable.Rewards),
            JournalItemUnlockedEntryEF journalItemUnlocked => new Rewards().AddItem(journalItemUnlocked.ItemId, 0),
            CraftingCompletedEntryEF craftingCompleted => Rewards.FromDBRewardsModel(craftingCompleted.Rewards),
            SmeltingCompletedEntryEF smeltingCompleted => Rewards.FromDBRewardsModel(smeltingCompleted.Rewards),
            BoostActivatedEntryEF => new Rewards(),
            _ => throw new InvalidDataException($"Unknown ActivityLog.Entry '{entry?.GetType()?.ToString() ?? "null"}'"),
        };

        Dictionary<string, string> properties = [];
        switch (entry)
        {
            case BoostActivatedEntryEF boostActivated:
                {
                    properties["boostId"] = boostActivated.ItemId.ToString();
                }

                break;
        }

        return new Types.Journal.JournalRecord.ActivityLogEntry(
            Types.Journal.JournalRecord.ActivityLogEntry.Type.FromDb(entry),
            TimeFormatter.FormatTime(entry.Timestamp),
            rewards.ToApiResponse(),
            properties
        );
    }
}
