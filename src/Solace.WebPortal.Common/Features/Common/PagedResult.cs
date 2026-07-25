namespace Solace.WebPortal.Common.Features.Common;

public record PagedResult<TData>(
    TData Data,
    int TotalCount
);
