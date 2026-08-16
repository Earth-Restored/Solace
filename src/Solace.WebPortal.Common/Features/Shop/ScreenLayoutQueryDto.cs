namespace Solace.WebPortal.Common.Features.Shop;

public sealed record ScreenLayoutQueryDto(Guid ComponentId, ColumnTypeDto ColumnType, IEnumerable<QueryDto> Queries);
