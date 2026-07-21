using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
using Solace.Db.Earth.Models.Common;

namespace Solace.Db.Earth.Models.Player;

#pragma warning disable MA0048 // File name must match type name
public abstract class TokenEF
{
    // for EF
    protected TokenEF()
    {
        AccountId = default;
    }

    [SetsRequiredMembers]
    protected TokenEF(Guid accountId)
    {
        AccountId = accountId;
    }

    public required Guid AccountId { get; set; }

    public Guid TokenId { get; set; }

    public Account Account { get; set; } = null!;
}

public abstract class RewardedTokenEF : TokenEF
{
    protected RewardedTokenEF()
    {
        Rewards = null!;
    }

    [SetsRequiredMembers]
    protected RewardedTokenEF(Guid accountId, Rewards rewards)
        : base(accountId)
    {
        Rewards = rewards;
    }

    public Rewards Rewards { get; init; }
}

public sealed class LevelUpTokenEF : RewardedTokenEF
{
    private LevelUpTokenEF()
        : base()
    {
    }

    [SetsRequiredMembers]
    public LevelUpTokenEF(Guid accountId, int level, Rewards rewards)
        : base(accountId, rewards)
    {
        Level = level;
        Rewards = rewards;
    }

    public int Level { get; init; }
}

public sealed class JournalItemUnlockedTokenEF : TokenEF
{
    private JournalItemUnlockedTokenEF()
        : base()
    {
    }

    [SetsRequiredMembers]
    public JournalItemUnlockedTokenEF(Guid accountId, Guid itemId)
        : base(accountId)
    {
        ItemId = itemId;
    }

    public Guid ItemId { get; init; }
}

public sealed class DailyLoginTokenEF : RewardedTokenEF
{
    private DailyLoginTokenEF()
        : base()
    {
    }

    [SetsRequiredMembers]
    public DailyLoginTokenEF(Guid accountId, DateOnly date, Rewards rewards, DateTimeOffset? claimedOn = null)
        : base(accountId, rewards)
    {
        Date = date;
        Rewards = rewards;
        ClaimedOn = claimedOn;
    }

    public DateOnly Date { get; init; }

    public DateTimeOffset? ClaimedOn { get; init; }

    [NotMapped, JsonIgnore, MemberNotNullWhen(true, nameof(ClaimedOn))]
    public bool Claimed => ClaimedOn is not null;
}
#pragma warning restore MA0048 // File name must match type name
