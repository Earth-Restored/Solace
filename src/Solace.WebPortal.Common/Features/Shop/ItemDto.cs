namespace Solace.WebPortal.Common.Features.Shop;

public sealed record ItemDto(
    Guid Id,
    string Title,
    IReadOnlyDictionary<string, string> TitleTranslations,
    string Description,
    IReadOnlyDictionary<string, string> DescriptionTranslations,
    bool Purchasable,
    bool Discount,
    DateTimeOffset StartDate,
    Guid? ThumbnailImageId,
    ItemDataTypeDto ItemDataType,
    BuildplateDto? BuildplateData,
    InventoryItemDto? InventoryItemData
);
