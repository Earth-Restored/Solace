using System.Diagnostics.CodeAnalysis;

namespace Solace.Db.Earth.Models.Player;

#pragma warning disable MA0048 // File name must match type name
public sealed class StackableItemEF
{
    // ef
    private StackableItemEF()
    {
    }

    [SetsRequiredMembers]
    public StackableItemEF(Guid accountId, Guid itemId, int count)
    {
        ProfileId = accountId;
        ItemId = itemId;
        Count = count;
    }

    public required Guid ProfileId { get; set; }

    public required Guid ItemId { get; set; }

    public required int Count { get; set; }

    public ProfileEF Profile { get; set; } = null!;
}

public sealed class NonStackableItemInstanceEF
{
    // ef
    private NonStackableItemInstanceEF()
    {
    }

    [SetsRequiredMembers]
    public NonStackableItemInstanceEF(Guid accountId, Guid itemId, Guid instanceId, int wear)
    {
        ProfileId = accountId;
        ItemId = itemId;
        InstanceId = instanceId;
        Wear = wear;
    }

    public required Guid ProfileId { get; set; }

    public required Guid ItemId { get; set; }

    public required Guid InstanceId { get; set; }

    public int Wear { get; set; }

    public ProfileEF Profile { get; set; } = null!;
}
#pragma warning restore MA0048 // File name must match type name
