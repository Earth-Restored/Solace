using Solace.Common;

namespace Solace.Db.Playfab.Models.Tabs;

public sealed class ScreenLayoutQueryEF : IEquatable<ScreenLayoutQueryEF>, ICloneable<ScreenLayoutQueryEF>
{
    public Guid Id { get; set; }

    public ColumnTypeEF ColumnType { get; set; }

    public Guid ComponentId { get; set; }

    public List<QueryEF> Queries { get; set; } = [];

    public ScreenLayoutQueryEF DeepCopy()
        => new()
        {
            Id = Id,
            ColumnType = ColumnType,
            ComponentId = ComponentId,
            Queries = [.. Queries.Select(static item => item.DeepCopy())],
        };

    public bool Equals(ScreenLayoutQueryEF? other)
        => other is not null && Id == other.Id && ColumnType == other.ColumnType && ComponentId == other.ComponentId && Queries.SequenceEqual(other.Queries);

    public override bool Equals(object? obj)
        => Equals(obj as ScreenLayoutQueryEF);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Id);
        hash.Add(ColumnType);
        hash.Add(ComponentId);

        foreach (var item in Queries)
        {
            hash.Add(item);
        }

        return hash.ToHashCode();
    }
}
