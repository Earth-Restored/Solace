namespace Solace.WebPortal.Common.Features.Store;

public sealed record ScreenLayoutQueryDto(Guid ComponentId, ColumnTypeDto ColumnType, IEnumerable<QueryDto> Queries);
