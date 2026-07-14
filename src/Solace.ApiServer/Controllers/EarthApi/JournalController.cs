using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Solace.ApiServer.Exceptions;
using Solace.ApiServer.Utils;
using Solace.Common;
using Solace.Common.Utils;
using Solace.DB;
using Solace.DB.Models.Player;
using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Solace.DB.Utils;
using Solace.ApiServer.Types.Journal;

namespace Solace.ApiServer.Controllers.EarthApi;

[Authorize]
[ApiVersion("1.1")]
[Route("1/api/v{version:apiVersion}/player/journal")]
internal sealed class JournalController : SolaceControllerBase
{
    private readonly EarthDbContext _earthDB;

    public JournalController(EarthDbContext earthDB)
    {
        _earthDB = earthDB;
    }

    [HttpGet]
    public async Task<Results<ContentHttpResult, BadRequest>> Get(CancellationToken cancellationToken)
    {
        if (!TryGetAccountId(out var accountId))
        {
            return TypedResults.BadRequest();
        }

        var journalEnties = _earthDB.JournalEntries
            .AsNoTracking()
            .Where(entry => entry.AccountId == accountId)
            .Select(entry => new { entry.ItemId, entry.FirstSeen, entry.LastSeen, entry.AmountCollected, })
            .AsAsyncEnumerable()
            .WithCancellation(cancellationToken);

        var activityLogEntries = _earthDB.ActivityLogs
            .AsNoTracking()
            .Where(entry => entry.AccountId == accountId)
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
