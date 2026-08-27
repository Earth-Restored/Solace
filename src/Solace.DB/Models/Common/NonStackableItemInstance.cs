using Solace.Common;

namespace Solace.DB.Models.Common;

public sealed record NonStackableItemInstance(
    string InstanceId,
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
