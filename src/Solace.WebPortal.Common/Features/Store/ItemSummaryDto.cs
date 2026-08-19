namespace Solace.WebPortal.Common.Features.Store;

public sealed record ItemSummaryDto(
    Guid Id,
    string Title,
    bool Purchasable,
    DateTimeOffset StartDate,
    ItemDataTypeDto ItemDataType
);
