namespace Solace.WebPortal.Common.Features.Players.Inventory;

public sealed record AddItemCommand(
    Guid ItemId,
    int Count
);
