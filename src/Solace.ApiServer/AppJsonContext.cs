using System.Text.Json.Serialization;
using Solace.ApiServer.Controllers;
using Solace.ApiServer.Types.Boost;
using Solace.ApiServer.Types.Buildplates;
using Solace.ApiServer.Types.Catalog;
using Solace.ApiServer.Types.Common;
using Solace.ApiServer.Types.Inventory;
using Solace.ApiServer.Types.Journal;
using Solace.ApiServer.Types.Profile;
using Solace.ApiServer.Types.Store;
using Solace.ApiServer.Types.Tappables;
using Solace.ApiServer.Types.Workshop;

namespace Solace.ApiServer;

[JsonSourceGenerationOptions(WriteIndented = false, PropertyNameCaseInsensitive = true, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(Boosts))]
[JsonSerializable(typeof(BuildplateInstance))]
[JsonSerializable(typeof(BuildplatesController.EncounterInstanceRequest))]
[JsonSerializable(typeof(BuildplatesController.SharedBuildplateInstanceRequest))]
[JsonSerializable(typeof(ChallengesController.ChallengeRecord))]
[JsonSerializable(typeof(ChallengesController.ChallengesResponse))]
[JsonSerializable(typeof(CraftingSlot))]
[JsonSerializable(typeof(Dictionary<Guid, EncounterState>))]
[JsonSerializable(typeof(Dictionary<string, object>))]
[JsonSerializable(typeof(EnvironmentSettingsController.FeatureFlags))]
[JsonSerializable(typeof(EnvironmentSettingsController.SettingsResponse))]
[JsonSerializable(typeof(ExpectedPurchasePriceR))]
[JsonSerializable(typeof(FinishPrice))]
[JsonSerializable(typeof(HotbarItem[]))]
[JsonSerializable(typeof(int))]
[JsonSerializable(typeof(InventoryController.SetHotbarRequestItem[]))]
[JsonSerializable(typeof(InventoryResponse))]
[JsonSerializable(typeof(ItemsCatalog))]
[JsonSerializable(typeof(JournalCatalog))]
[JsonSerializable(typeof(JournalRecord))]
[JsonSerializable(typeof(List<OwnedBuildplate>))]
[JsonSerializable(typeof(List<StoreItemInfo>))]
[JsonSerializable(typeof(NFCBoost[]))]
[JsonSerializable(typeof(object))]
[JsonSerializable(typeof(ProductsController.GetProductInfoRequest))]
[JsonSerializable(typeof(ProductsController.ProductInfo))]
[JsonSerializable(typeof(ProfileResponse))]
[JsonSerializable(typeof(RecipesCatalog))]
[JsonSerializable(typeof(ResourcePackController.ResourcePackResponse[]))]
[JsonSerializable(typeof(SharedBuildplate))]
[JsonSerializable(typeof(SigninController.SigninRequest))]
[JsonSerializable(typeof(SigninController.SignInResponse))]
[JsonSerializable(typeof(SmeltingSlot))]
[JsonSerializable(typeof(SplitRubies))]
[JsonSerializable(typeof(StoreController.PurchaseItemRequest))]
[JsonSerializable(typeof(StoreController.StoreItemInfoRequest[]))]
[JsonSerializable(typeof(TappablesController.RedeemTappableResponse))]
[JsonSerializable(typeof(TappablesController.TappableRequest))]
[JsonSerializable(typeof(TappablesController.TappablesResponse))]
[JsonSerializable(typeof(Token))]
[JsonSerializable(typeof(TokensController.TokensResponse))]
[JsonSerializable(typeof(WorkshopController.CollectItemsResponse))]
[JsonSerializable(typeof(WorkshopController.StartRequestCrafting))]
[JsonSerializable(typeof(WorkshopController.StartRequestSmelting))]
[JsonSerializable(typeof(WorkshopController.UtilityBlocksResponse))]
internal sealed partial class AppJsonContext : JsonSerializerContext
{
}
