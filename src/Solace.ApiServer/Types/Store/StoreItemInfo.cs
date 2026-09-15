using Solace.ApiServer.Types.Buildplates;

namespace Solace.ApiServer.Types.Store;

internal sealed record StoreItemInfo(
    Guid Id,
    StoreItemType StoreItemType,
    StoreItemStatus? Status,
    uint StreamVersion,
    string? Model,
    Offset? BuildplateWorldOffset,
    Dimension? BuildplateWorldDimension,
    IReadOnlyDictionary<Guid, int>? InventoryCounts,
    Guid? FeaturedItem
);
