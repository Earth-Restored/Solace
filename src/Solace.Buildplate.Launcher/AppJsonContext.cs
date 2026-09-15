using System.Text.Json.Serialization;
using Solace.Buildplate.Connector.Model;
using Solace.Buildplate.Model;

namespace Solace.Buildplate.Launcher;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(bool))]
[JsonSerializable(typeof(bool?))]
[JsonSerializable(typeof(Buildplate.PreviewGenerator.PreviewModel))]
[JsonSerializable(typeof(ConnectorPluginArg))]
[JsonSerializable(typeof(FindPlayerIdRequest))]
[JsonSerializable(typeof(InitialPlayerStateResponse))]
[JsonSerializable(typeof(InitialPlayerStateResponse))]
[JsonSerializable(typeof(Instance.BuildplateLoadRequest))]
[JsonSerializable(typeof(Instance.BuildplateLoadResponse))]
[JsonSerializable(typeof(Instance.EncounterBuildplateLoadRequest))]
[JsonSerializable(typeof(Instance.RequestWithInstanceId<InventoryAddItemMessage>))]
[JsonSerializable(typeof(Instance.RequestWithInstanceId<InventoryRemoveItemRequest>))]
[JsonSerializable(typeof(Instance.RequestWithInstanceId<InventorySetHotbarMessage>))]
[JsonSerializable(typeof(Instance.RequestWithInstanceId<InventoryUpdateItemWearMessage>))]
[JsonSerializable(typeof(Instance.RequestWithInstanceId<PlayerConnectedRequest>))]
[JsonSerializable(typeof(Instance.RequestWithInstanceId<PlayerDisconnectedRequest>))]
[JsonSerializable(typeof(Instance.RequestWithInstanceId<string>))]
[JsonSerializable(typeof(Instance.RequestWithInstanceId<WorldSavedMessage>))]
[JsonSerializable(typeof(Instance.SharedBuildplateLoadRequest))]
[JsonSerializable(typeof(InstanceManager.StartNotification))]
[JsonSerializable(typeof(InstanceManager.StartRequest))]
[JsonSerializable(typeof(int?))]
[JsonSerializable(typeof(InventoryAddItemMessage))]
[JsonSerializable(typeof(InventoryRemoveItemRequest))]
[JsonSerializable(typeof(InventoryResponse))]
[JsonSerializable(typeof(InventorySetHotbarMessage))]
[JsonSerializable(typeof(InventoryUpdateItemWearMessage))]
[JsonSerializable(typeof(PlayerConnectedRequest))]
[JsonSerializable(typeof(PlayerConnectedResponse))]
[JsonSerializable(typeof(PlayerDisconnectedRequest))]
[JsonSerializable(typeof(PlayerDisconnectedResponse))]
[JsonSerializable(typeof(PreviewRequest))]
[JsonSerializable(typeof(string))]
[JsonSerializable(typeof(WorldSavedMessage))]
internal sealed partial class AppJsonContext : JsonSerializerContext
{
}
