namespace Solace.ApiServer.Types.Inventory;

internal sealed record InventoryResponse(
    HotbarItem?[] Hotbar,
    StackableInventoryItem[] StackableItems,
    NonStackableInventoryItem[] NonStackableItems
);