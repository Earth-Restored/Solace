namespace Solace.WebPortal.Common.Features.Players.Inventory;

public sealed record NonStackableItemDto(
    Guid ItemId,
    int Wear,
    Guid InstanceId
);
