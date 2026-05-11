namespace Solace.DB.Models.Common;

public sealed record NonStackableItemInstance(
    string InstanceId,
    int Wear
)
{
    public sealed record Legacy(
        string InstanceId,
        int Wear
    );
}
