using System.Text.Json.Serialization;
using Solace.ApiServer.Types.Buildplates;

namespace Solace.ApiServer.Types.Store;

internal sealed record StoreItemInfo(
    Guid Id,
    StoreItemInfo.StoreItemTypeE StoreItemType,
    StoreItemInfo.StoreItemStatus? Status,
    uint StreamVersion,
    string? Model,
    Offset? BuildplateWorldOffset,
    Dimension? BuildplateWorldDimension,
    IReadOnlyDictionary<Guid, int>? InventoryCounts,
    Guid? FeaturedItem
)
{
    [JsonConverter(typeof(JsonStringEnumConverter<StoreItemTypeE>))]
    internal enum StoreItemTypeE
    {
        Buildplates,
        Items,
    }

    [JsonConverter(typeof(JsonStringEnumConverter<StoreItemStatus>))]
    internal enum StoreItemStatus
    {
        Found,
        NotFound,
        NotModified,
    }
}
