using Solace.Common;

namespace Solace.Db.Earth.Models.Common;

public sealed record NonStackableItemInstance(
    Guid InstanceId,
    int Wear
) : ICloneable<NonStackableItemInstance>
{
    public NonStackableItemInstance DeepCopy()
        => new(this);

    public sealed record Legacy(
        string InstanceId,
        int Wear
    );
}