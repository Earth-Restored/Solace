namespace Solace.WebPortal.Common.Features.Players.Inventory;

public sealed record UpdateItemCommand(
    Guid ItemId,
    Guid? InstanceId,
    int? Count,
    int? Wear
);
