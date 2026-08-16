namespace Solace.WebPortal.Client.Features.Shop.Models;

public sealed class QueryViewModel
{
    public List<QueryContentType> QueryContentTypes { get; init; } = [];

    public List<Guid> ProductIds { get; init; } = [];
}
