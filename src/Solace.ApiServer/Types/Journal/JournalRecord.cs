using System.Diagnostics;
using System.Text.Json.Serialization;
using Solace.ApiServer.Types.Common;
using static Solace.ApiServer.Types.Journal.JournalRecord;

namespace Solace.ApiServer.Types.Journal;

internal sealed record JournalRecord(
    Dictionary<Guid, InventoryJournalEntry> InventoryJournal,
    ActivityLogEntry[] ActivityLog
)
{
    internal sealed record InventoryJournalEntry(
        string FirstSeen,
        string LastSeen,
        int AmountCollected
    );

    internal sealed record ActivityLogEntry(
        ActivityLogEntryType Scenario,
        string EventTime,
        Rewards Rewards,
        Dictionary<string, string> Properties
    );

    [JsonConverter(typeof(JsonStringEnumConverter<ActivityLogEntryType>))]
    internal enum ActivityLogEntryType
    {
#pragma warning disable CA1707 // Identifiers should not contain underscores
        [JsonStringEnumMemberName("LevelUp")] LEVEL_UP,
        [JsonStringEnumMemberName("TappableCollected")] TAPPABLE,
        [JsonStringEnumMemberName("JournalContentCollected")] JOURNAL_ITEM_UNLOCKED,
        [JsonStringEnumMemberName("CraftingJobCompleted")] CRAFTING_COMPLETED,
        [JsonStringEnumMemberName("SmeltingJobCompleted")] SMELTING_COMPLETED,
        [JsonStringEnumMemberName("BoostActivated")] BOOST_ACTIVATED,
#pragma warning restore CA1707 // Identifiers should not contain underscores
    }
}

#pragma warning disable MA0048 // File name must match type name
internal static class ActivityLogTypeExtensions
#pragma warning restore MA0048 // File name must match type name
{
    extension(ActivityLogEntryType)
    {
        public static ActivityLogEntryType FromDb(Db.Earth.Models.Player.ActivityLogEntryEF entry)
            => entry switch
            {
                Db.Earth.Models.Player.LevelUpEntryEF => ActivityLogEntryType.LEVEL_UP,
                Db.Earth.Models.Player.TappableEntryEF => ActivityLogEntryType.TAPPABLE,
                Db.Earth.Models.Player.JournalItemUnlockedEntryEF => ActivityLogEntryType.JOURNAL_ITEM_UNLOCKED,
                Db.Earth.Models.Player.CraftingCompletedEntryEF => ActivityLogEntryType.CRAFTING_COMPLETED,
                Db.Earth.Models.Player.SmeltingCompletedEntryEF => ActivityLogEntryType.SMELTING_COMPLETED,
                Db.Earth.Models.Player.BoostActivatedEntryEF => ActivityLogEntryType.BOOST_ACTIVATED,
                _ => throw new UnreachableException(),
            };
    }
}