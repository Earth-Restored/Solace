using System.Text.Json.Serialization;
using Solace.ApiServer.Controllers;
using Solace.ApiServer.Types.Common;

namespace Solace.ApiServer;

[JsonSourceGenerationOptions(WriteIndented = false, PropertyNameCaseInsensitive = true, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(SigninController.SigninRequest))]
[JsonSerializable(typeof(WorkshopController.StartRequestCrafting))]
[JsonSerializable(typeof(WorkshopController.StartRequestSmelting))]
[JsonSerializable(typeof(StoreController.StoreItemInfoRequest[]))]
[JsonSerializable(typeof(StoreController.PurchaseItemRequest))]
[JsonSerializable(typeof(TappablesController.TappableRequest))]
[JsonSerializable(typeof(InventoryController.SetHotbarRequestItem[]))]
[JsonSerializable(typeof(BuildplatesController.EncounterInstanceRequest))]
[JsonSerializable(typeof(BuildplatesController.SharedBuildplateInstanceRequest))]
[JsonSerializable(typeof(ProductsController.GetProductInfoRequest))]
[JsonSerializable(typeof(ExpectedPurchasePriceR))]
[JsonSerializable(typeof(Dictionary<string, object>))]
internal sealed partial class AppJsonContext : JsonSerializerContext
{
}
