using Solace.Common;

namespace Solace.Db.Playfab.Models.Tabs;

public sealed class QueryEF : IEquatable<QueryEF>, ICloneable<QueryEF>
{
    public Guid Id { get; set; }

    public int TopCount { get; set; }

    public List<Guid> ProductIds { get; set; } = [];

    public List<ContentTypeEF> QueryContentTypes { get; set; } = [];

    public QueryEF DeepCopy() => throw new NotImplementedException();

    public bool Equals(QueryEF? other) => throw new NotImplementedException();

    public override bool Equals(object obj)
    {
        return Equals(obj as QueryEF);
    }

    public override int GetHashCode()
    {
        throw new NotImplementedException();
    }
}
