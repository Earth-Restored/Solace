using Solace.Common;

namespace Solace.Db.Playfab.Models.Tabs;

public sealed class QueryEF : IEquatable<QueryEF>, ICloneable<QueryEF>
{
    public Guid Id { get; set; }

    public int TopCount { get; set; }

    public List<Guid> ProductIds { get; set; } = [];

    public List<ContentTypeEF> QueryContentTypes { get; set; } = [];

    public QueryEF DeepCopy()
        => new()
        {
            Id = Id,
            TopCount = TopCount,
            ProductIds = [.. ProductIds],
            QueryContentTypes = [.. QueryContentTypes],
        };

    public bool Equals(QueryEF? other)
        => other is not null && Id == other.Id && TopCount == other.TopCount && ProductIds.SequenceEqual(other.ProductIds) && QueryContentTypes.SequenceEqual(other.QueryContentTypes);

    public override bool Equals(object? obj)
        => Equals(obj as QueryEF);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Id);
        hash.Add(TopCount);

        foreach (var item in ProductIds)
        {
            hash.Add(item);
        }

        foreach (var item in QueryContentTypes)
        {
            hash.Add(item);
        }

        return hash.ToHashCode();
    }
}
