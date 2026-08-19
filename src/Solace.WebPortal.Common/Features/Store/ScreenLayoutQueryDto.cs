namespace Solace.WebPortal.Common.Features.Store;

public sealed record ScreenLayoutQueryDto(ColumnTypeDto ColumnType, IEnumerable<QueryDto> Queries);
