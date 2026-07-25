namespace Solace.WebPortal.Common.Features.Common;

public sealed record PagedSearchResult<TData>(
    TData Data,
    int TotalCount, // total unfiltered count
    int MatchingCount // filtered count
) : PagedResult<TData>(Data, TotalCount);
