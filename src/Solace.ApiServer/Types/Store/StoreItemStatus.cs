using System.Text.Json.Serialization;

namespace Solace.ApiServer.Types.Store;

[JsonConverter(typeof(JsonStringEnumConverter<StoreItemStatus>))]
internal enum StoreItemStatus
{
    Found,
    NotFound,
    NotModified,
}
