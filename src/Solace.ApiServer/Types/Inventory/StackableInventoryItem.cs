namespace Solace.ApiServer.Types.Inventory;

internal sealed record StackableInventoryItem(
    Guid Id,
    int Owned,
    int Fragments,
    StackableInventoryItemTime Unlocked,
    StackableInventoryItemTime Seen
);
