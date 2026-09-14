#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Solace.Buildplate.Connector.Model;

public sealed record InventorySetHotbarMessageItem(
    Guid ItemId,
    int Count,
    Guid? InstanceId
);
