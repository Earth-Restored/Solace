namespace Solace.DB.Earth.Models.Player;

public sealed class ItemJournalEntryEF
{
    public required Guid AccountId { get; set; }

    public required Guid ItemId { get; set; }

    public required DateTimeOffset FirstSeen { get; set; }

    public required DateTimeOffset LastSeen { get; set; }

    public required int AmountCollected { get; set; }

    public Account Account { get; set; } = null!;
}