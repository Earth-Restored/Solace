namespace Solace.ApiServer.Types.Inventory;

internal sealed record NonStackableInventoryItem(
    Guid Id,
    NonStackableInventoryItemInstance[] Instances,
    int Fragments,
    NonStackableInventoryItemTime Unlocked,
    NonStackableInventoryItemTime Seen
);
