using System.Text.Json.Serialization;
using Solace.Buildplate.Connector.Model;
using Solace.Buildplate.Model;

namespace Solace.Buildplate.Launcher;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(PreviewRequest))]
[JsonSerializable(typeof(InstanceManager.StartNotification))]
[JsonSerializable(typeof(InstanceManager.StartRequest))]
[JsonSerializable(typeof(Instance.BuildplateLoadRequest))]
[JsonSerializable(typeof(Instance.BuildplateLoadResponse))]
[JsonSerializable(typeof(Instance.SharedBuildplateLoadRequest))]
[JsonSerializable(typeof(Instance.EncounterBuildplateLoadRequest))]
[JsonSerializable(typeof(ConnectorPluginArg))]
[JsonSerializable(typeof(WorldSavedMessage))]
[JsonSerializable(typeof(InventoryAddItemMessage))]
[JsonSerializable(typeof(InventoryUpdateItemWearMessage))]
[JsonSerializable(typeof(InventorySetHotbarMessage))]
[JsonSerializable(typeof(PlayerConnectedRequest))]
[JsonSerializable(typeof(PlayerConnectedResponse))]
[JsonSerializable(typeof(PlayerDisconnectedRequest))]
[JsonSerializable(typeof(PlayerDisconnectedResponse))]
[JsonSerializable(typeof(string))]
[JsonSerializable(typeof(bool))]
[JsonSerializable(typeof(bool?))]
[JsonSerializable(typeof(int?))]
[JsonSerializable(typeof(InventoryResponse))]
[JsonSerializable(typeof(InventoryRemoveItemRequest))]
[JsonSerializable(typeof(FindPlayerIdRequest))]
[JsonSerializable(typeof(InitialPlayerStateResponse))]
[JsonSerializable(typeof(InitialPlayerStateResponse))]
[JsonSerializable(typeof(Buildplate.PreviewGenerator.PreviewModel))]
internal sealed partial class AppJsonContext : JsonSerializerContext
{
}
