namespace Solace.ApiServer.Types.Inventory;

internal sealed record NonStackableInventoryItemInstance(
    Guid Id,
    float Health
);
