using Solace.Common;

namespace Solace.Db.Playfab.Models.Tabs;

public sealed class ScreenLayoutQueryEF : IEquatable<ScreenLayoutQueryEF>, ICloneable<ScreenLayoutQueryEF>
{
    public Guid Id { get; set; }

    public ColumnTypeEF ColumnType { get; set; }

    public Guid ComponentId { get; set; }

    public List<QueryEF> Queries { get; set; } = [];

    public ScreenLayoutQueryEF DeepCopy() => throw new NotImplementedException();

    public bool Equals(ScreenLayoutQueryEF? other) => throw new NotImplementedException();

    public override bool Equals(object obj)
    {
        return Equals(obj as ScreenLayoutQueryEF);
    }

    public override int GetHashCode()
    {
        throw new NotImplementedException();
    }
}
