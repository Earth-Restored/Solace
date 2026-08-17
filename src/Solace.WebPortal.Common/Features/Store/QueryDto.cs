namespace Solace.WebPortal.Common.Features.Store;

public sealed record QueryDto(int TopCount, IEnumerable<QueryContentTypeDto> QueryContentTypes, IEnumerable<Guid> ProductIds);
