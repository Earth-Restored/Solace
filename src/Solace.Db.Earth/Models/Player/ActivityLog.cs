using System.Diagnostics.CodeAnalysis;
using Solace.Db.Earth.Models.Common;

namespace Solace.Db.Earth.Models.Player;

#pragma warning disable MA0048 // File name must match type name
public abstract class ActivityLogEntryEF
{
    protected ActivityLogEntryEF()
    {
    }

    [SetsRequiredMembers]
    protected ActivityLogEntryEF(Guid accountId, DateTimeOffset timestamp)
    {
        AccountId = accountId;
        Timestamp = timestamp;
    }

    public required Guid AccountId { get; set; }

    public long EntryId { get; set; }

    public required DateTimeOffset Timestamp { get; init; }

    public Account Account { get; set; } = null!;
}

public abstract class RewardedActivityLogEntryEF : ActivityLogEntryEF
{
    protected RewardedActivityLogEntryEF()
        : base()
    {
        Rewards = null!;
    }

    [SetsRequiredMembers]
    protected RewardedActivityLogEntryEF(Guid accountId, DateTimeOffset timestamp, Rewards rewards)
        : base(accountId, timestamp)
    {
        Rewards = rewards;
    }

    public Rewards Rewards { get; init; }
}

public sealed class LevelUpEntryEF : ActivityLogEntryEF
{
    private LevelUpEntryEF()
        : base()
    {
    }

    [SetsRequiredMembers]
    public LevelUpEntryEF(Guid accountId, DateTimeOffset timestamp, int level)
        : base(accountId, timestamp)
    {
        Level = level;
    }

    public int Level { get; init; }
}

public sealed class TappableEntryEF : RewardedActivityLogEntryEF
{
    private TappableEntryEF()
         : base()
    {
    }

    [SetsRequiredMembers]
    public TappableEntryEF(Guid accountId, DateTimeOffset timestamp, Rewards rewards)
        : base(accountId, timestamp, rewards)
    {
    }
}

public sealed class JournalItemUnlockedEntryEF : ActivityLogEntryEF
{
    private JournalItemUnlockedEntryEF()
         : base()
    {
    }

    [SetsRequiredMembers]
    public JournalItemUnlockedEntryEF(Guid accountId, DateTimeOffset timestamp, Guid itemId)
        : base(accountId, timestamp)
    {
        ItemId = itemId;
    }

    public Guid ItemId { get; init; }
}

public sealed class CraftingCompletedEntryEF : RewardedActivityLogEntryEF
{
    private CraftingCompletedEntryEF()
         : base()
    {
    }

    [SetsRequiredMembers]
    public CraftingCompletedEntryEF(Guid accountId, DateTimeOffset timestamp, Rewards rewards)
        : base(accountId, timestamp, rewards)
    {
    }
}

public sealed class SmeltingCompletedEntryEF : RewardedActivityLogEntryEF
{
    private SmeltingCompletedEntryEF()
         : base()
    {
    }

    [SetsRequiredMembers]
    public SmeltingCompletedEntryEF(Guid accountId, DateTimeOffset timestamp, Rewards rewards)
        : base(accountId, timestamp, rewards)
    {
    }
}

public sealed class BoostActivatedEntryEF : ActivityLogEntryEF
{
    private BoostActivatedEntryEF()
         : base()
    {
    }

    [SetsRequiredMembers]
    public BoostActivatedEntryEF(Guid accountId, DateTimeOffset timestamp, Guid itemId)
        : base(accountId, timestamp)
    {
        ItemId = itemId;
    }

    public Guid ItemId { get; init; }
}
#pragma warning restore MA0048 // File name must match type name
