namespace Solace.WebPortal.Client.Features.Shop;

public sealed class QueryViewModel
{
    public int TopCount { get; set; } = 25;

    public List<string> QueryContentTypes { get; init; } = [];

    public List<Guid> ProductIds { get; init; } = [];
}
