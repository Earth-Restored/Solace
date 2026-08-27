using System.ComponentModel;
using System.Text.Json.Serialization;
using Solace.ApiServer.Types.Common;
using static Solace.ApiServer.Types.Journal.JournalRecord;

namespace Solace.ApiServer.Types.Journal;

public sealed record JournalRecord(
     Dictionary<string, InventoryJournalEntry> InventoryJournal,
     ActivityLogEntry[] ActivityLog
)
{
    public sealed record InventoryJournalEntry(
        string FirstSeen,
        string LastSeen,
        int AmountCollected
    );

    public sealed record ActivityLogEntry(
        ActivityLogEntry.Type Scenario,
        string EventTime,
        Rewards Rewards,
        Dictionary<string, string> Properties
    )
    {
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public enum Type
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

public static class ActivityLogTypeExtensions
{
    extension (ActivityLogEntry.Type)
    {
        public static ActivityLogEntry.Type FromDb(DB.Models.Player.ActivityLogEF.Entry.TypeE type)
            => type switch
            {
                DB.Models.Player.ActivityLogEF.Entry.TypeE.LEVEL_UP => ActivityLogEntry.Type.LEVEL_UP,
                DB.Models.Player.ActivityLogEF.Entry.TypeE.TAPPABLE => ActivityLogEntry.Type.TAPPABLE,
                DB.Models.Player.ActivityLogEF.Entry.TypeE.JOURNAL_ITEM_UNLOCKED => ActivityLogEntry.Type.JOURNAL_ITEM_UNLOCKED,
                DB.Models.Player.ActivityLogEF.Entry.TypeE.CRAFTING_COMPLETED => ActivityLogEntry.Type.CRAFTING_COMPLETED,
                DB.Models.Player.ActivityLogEF.Entry.TypeE.SMELTING_COMPLETED => ActivityLogEntry.Type.SMELTING_COMPLETED,
                DB.Models.Player.ActivityLogEF.Entry.TypeE.BOOST_ACTIVATED => ActivityLogEntry.Type.BOOST_ACTIVATED,
                _ => throw new InvalidEnumArgumentException(nameof(type), (int)type, typeof(DB.Models.Player.ActivityLogEF.Entry.TypeE)),
            };
    }
}