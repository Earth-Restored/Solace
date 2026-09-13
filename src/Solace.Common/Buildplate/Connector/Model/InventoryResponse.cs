#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Solace.Buildplate.Connector.Model;
#pragma warning restore IDE0130 // Namespace does not match folder structure

public sealed record InventoryResponse(
    InventoryResponseItem[] Items,
    InventoryResponseHotbarItem?[] Hotbar
);

public sealed record InventoryResponseItem(
    Guid Id,
    int Count,
    Guid? InstanceId,
    int Wear
);

public sealed record InventoryResponseHotbarItem(
    Guid Id,
    int Count,
    Guid? InstanceId
);
