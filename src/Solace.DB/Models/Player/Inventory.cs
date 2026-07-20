using System.Diagnostics.CodeAnalysis;

namespace Solace.DB.Models.Player;

public sealed class StackableItemEF
{
    // ef
    private StackableItemEF()
    {
    }
    
    [SetsRequiredMembers]
    public StackableItemEF(Guid accountId, Guid itemId, int count)
    {
        AccountId = accountId;
        ItemId = itemId;
        Count = count;
    }

    public required Guid AccountId { get; set; }

    public required Guid ItemId { get; set; }

    public required int Count { get; set; }

    public Account Account { get; set; } = null!;
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
        AccountId = accountId;
        ItemId = itemId;
        InstanceId = instanceId;
        Wear = wear;
    }

    public required Guid AccountId { get; set; }

    public required Guid ItemId { get; set; }

    public required Guid InstanceId { get; set; }

    public int Wear { get; set; }

    public Account Account { get; set; } = null!;
}
