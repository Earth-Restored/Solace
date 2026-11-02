using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
using Solace.Db.Earth.Models.Challenges;
using Solace.Db.Earth.Models.Common;

namespace Solace.Db.Earth.Models.Player;

#pragma warning disable MA0048 // File name must match type name
public abstract class TokenEF
{
    // for EF
    protected TokenEF()
    {
        ProfileId = default;
    }

    [SetsRequiredMembers]
    protected TokenEF(Guid accountId)
    {
        ProfileId = accountId;
    }

    public required Guid ProfileId { get; set; }

    public Guid TokenId { get; set; }

    public ProfileEF Profile { get; set; } = null!;
}

public abstract class RewardedTokenEF : TokenEF
{
    protected RewardedTokenEF()
    {
        Rewards = null!;
    }

    [SetsRequiredMembers]
    protected RewardedTokenEF(Guid profileId, Rewards rewards)
        : base(profileId)
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
    public LevelUpTokenEF(Guid profileId, int level, Rewards rewards)
        : base(profileId, rewards)
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

public sealed class FTUESeasonsTokenEF : TokenEF
{
    private FTUESeasonsTokenEF()
        : base()
    {
    }

    [SetsRequiredMembers]
    public FTUESeasonsTokenEF(Guid profileId, Guid seasonId)
        : base(profileId)
    {
        SeasonId = seasonId;
    }

    public Guid SeasonId { get; init; }
}

public sealed class NewSeasonNotificationTokenEF : TokenEF
{
    private NewSeasonNotificationTokenEF()
        : base()
    {
    }

    [SetsRequiredMembers]
    public NewSeasonNotificationTokenEF(Guid profileId, Guid seasonId)
        : base(profileId)
    {
        SeasonId = seasonId;
    }

    public Guid SeasonId { get; init; }
}

public sealed class OOBESeasonChallengeSelectedTokenEF : TokenEF
{
    private OOBESeasonChallengeSelectedTokenEF()
        : base()
    {
    }

    [SetsRequiredMembers]
    public OOBESeasonChallengeSelectedTokenEF(Guid profileId)
        : base(profileId)
    {
    }
}

public sealed class ChallengeProgressTokenEF : TokenEF
{
    private ChallengeProgressTokenEF()
        : base()
    {
    }

    [SetsRequiredMembers]
    public ChallengeProgressTokenEF(Guid profileId, Guid challengeId, int previousCount, int currentCount, int totalThreshold, ChallengeCategoryEF category)
        : base(profileId)
    {
        ChallengeId = challengeId;
        PreviousCount = previousCount;
        CurrentCount = currentCount;
        TotalThreshold = totalThreshold;
        Category = category;
    }

    public Guid ChallengeId { get; init; }

    public int PreviousCount { get; init; }

    public int CurrentCount { get; init; }

    public int TotalThreshold { get; init; }

    public ChallengeCategoryEF Category { get; init; }
}

public sealed class ChallengeCompleteTokenEF : TokenEF
{
    private ChallengeCompleteTokenEF()
        : base()
    {
    }

    [SetsRequiredMembers]
    public ChallengeCompleteTokenEF(Guid profileId, Guid challengeId, DateTimeOffset? expirationTime = null)
        : base(profileId)
    {
        ChallengeId = challengeId;
        ExpirationTime = expirationTime;
    }

    public Guid ChallengeId { get; init; }

    public DateTimeOffset? ExpirationTime { get; init; }
}
#pragma warning restore MA0048 // File name must match type name
