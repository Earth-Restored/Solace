namespace Solace.WebPortal.Common.Features.Shop;

public sealed record QueryDto(int TopCount, IEnumerable<QueryContentTypeDto> QueryContentTypes, IEnumerable<Guid> ProductIds);
