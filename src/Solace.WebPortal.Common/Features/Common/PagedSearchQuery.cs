namespace Solace.WebPortal.Common.Features.Common;

public sealed record PagedSearchQuery(
    string? SearchTerm = null,
    int Page = 1,
    int PageSize = 8
) : SearchQuery(SearchTerm);
