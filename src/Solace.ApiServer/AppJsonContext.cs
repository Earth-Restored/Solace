using System.Text.Json.Serialization;
using Solace.ApiServer.Controllers;
using Solace.ApiServer.Controllers.EarthApi;
using Solace.ApiServer.Types.Common;

namespace Solace.ApiServer;

[JsonSourceGenerationOptions(WriteIndented = false, PropertyNameCaseInsensitive = true, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(Controllers.PlayfabApi.AuthenticationController.GetEntityTokenRequest))]
[JsonSerializable(typeof(Controllers.PlayfabApi.EventController.WriteTelemetryEventsRequest))]
[JsonSerializable(typeof(Controllers.PlayfabApi.ClientController.GetUserPublisherDataRequest))]
[JsonSerializable(typeof(Controllers.PlayfabApi.ClientController.GetPlayerStatisticsRequest))]
[JsonSerializable(typeof(Controllers.PlayfabApi.ObjectController.GetObjectsRequest))]
[JsonSerializable(typeof(Controllers.PlayfabApi.ObjectController.SetObjectsRequest))]
[JsonSerializable(typeof(Controllers.PlayfabApi.LoginController.LoginWithCustomIDRequest))]
[JsonSerializable(typeof(Controllers.PlayfabApi.LoginController.LoginWithXboxRequest))]
[JsonSerializable(typeof(Controllers.PlayfabApi.CatalogController.CatalogSearchRequest))]
[JsonSerializable(typeof(Controllers.PlayfabApi.CatalogController.GetPublishedItemRequest))]
[JsonSerializable(typeof(SigninController.SigninRequest))]
[JsonSerializable(typeof(WorkshopController.StartRequestCrafting))]
[JsonSerializable(typeof(WorkshopController.StartRequestSmelting))]
[JsonSerializable(typeof(ShopController.StoreItemInfoRequest[]))]
[JsonSerializable(typeof(ShopController.PurchaseItemRequest))]
[JsonSerializable(typeof(TappablesController.TappableRequest))]
[JsonSerializable(typeof(InventoryController.SetHotbarRequestItem[]))]
[JsonSerializable(typeof(BuildplatesController.EncounterInstanceRequest))]
[JsonSerializable(typeof(BuildplatesController.SharedBuildplateInstanceRequest))]
[JsonSerializable(typeof(ExpectedPurchasePriceR))]
[JsonSerializable(typeof(Dictionary<string, object>))]
internal sealed partial class AppJsonContext : JsonSerializerContext
{
}
