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
        ActivityLogEntry.Type Scenario,
        string EventTime,
        Rewards Rewards,
        Dictionary<string, string> Properties
    )
    {
        [JsonConverter(typeof(JsonStringEnumConverter<Type>))]
        internal enum Type
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
}

#pragma warning disable MA0048 // File name must match type name
internal static class ActivityLogTypeExtensions
#pragma warning restore MA0048 // File name must match type name
{
    extension(ActivityLogEntry.Type)
    {
        public static ActivityLogEntry.Type FromDb(DB.Earth.Models.Player.ActivityLogEntryEF entry)
            => entry switch
            {
                DB.Earth.Models.Player.LevelUpEntryEF => ActivityLogEntry.Type.LEVEL_UP,
                DB.Earth.Models.Player.TappableEntryEF => ActivityLogEntry.Type.TAPPABLE,
                DB.Earth.Models.Player.JournalItemUnlockedEntryEF => ActivityLogEntry.Type.JOURNAL_ITEM_UNLOCKED,
                DB.Earth.Models.Player.CraftingCompletedEntryEF => ActivityLogEntry.Type.CRAFTING_COMPLETED,
                DB.Earth.Models.Player.SmeltingCompletedEntryEF => ActivityLogEntry.Type.SMELTING_COMPLETED,
                DB.Earth.Models.Player.BoostActivatedEntryEF => ActivityLogEntry.Type.BOOST_ACTIVATED,
                _ => throw new UnreachableException(),
            };
    }
}