namespace Solace.WebPortal.Common.Features.Players.Inventory;

public sealed record GetInventoryResponse(
    IReadOnlyList<StackableItemDto> StackableItems,
    IReadOnlyList<NonStackableItemDto> NonStackableItems
);
