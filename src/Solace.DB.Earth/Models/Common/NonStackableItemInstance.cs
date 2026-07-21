using Solace.Common;

namespace Solace.DB.Earth.Models.Common;

public sealed record NonStackableItemInstance(
    Guid InstanceId,
    int Wear
) : ICloneable<NonStackableItemInstance>
{
    public NonStackableItemInstance DeepCopy()
        => new NonStackableItemInstance(this);

    public sealed record Legacy(
        string InstanceId,
        int Wear
    );
}