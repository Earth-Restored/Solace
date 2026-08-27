using Solace.Common;

namespace Solace.Db.Migrator.Old.Earth.Models.Common;

public sealed record NonStackableItemInstance(
    string InstanceId,
    int Wear
) : ICloneable<NonStackableItemInstance>
{
    public NonStackableItemInstance DeepCopy()
        => new(this);
}
