namespace Solace.ApiServer.Types.Buildplates;

internal sealed record SharedBuildplate(
    string PlayerUsername,
    string SharedOn,
    SharedBuildplateData BuildplateData,
    Inventory.InventoryResponse Inventory
);
