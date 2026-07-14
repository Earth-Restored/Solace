using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;
using Solace.Common;
using Solace.Common.Utils;

namespace Solace.DB.Models.Player;

public sealed class ItemJournalEntryEF
{
    public required Guid AccountId { get; set; }

    public required Guid ItemId { get; set; }

    public required DateTimeOffset FirstSeen { get; set; }

    public required DateTimeOffset LastSeen { get; set; }

    public required int AmountCollected { get; set; }

    public Account Account { get; set; } = null!;
}