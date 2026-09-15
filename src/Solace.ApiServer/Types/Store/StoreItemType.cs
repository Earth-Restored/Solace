using System.Text.Json.Serialization;

namespace Solace.ApiServer.Types.Store;

[JsonConverter(typeof(JsonStringEnumConverter<StoreItemType>))]
internal enum StoreItemType
{
    Buildplates,
    Items,
}
