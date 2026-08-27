using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
using Solace.Common;
using Solace.Db.Migrator.Old.Earth.Models.Common;

namespace Solace.Db.Migrator.Old.Earth.Models.Player;

public sealed class TokensEF : IEntityWithId<Guid>, IVersionedEntity
{
    public Guid Id { get; set; }

    public int Version { get; set; } = 1;

    public Account Account { get; set; } = null!;

    public Dictionary<string, Token> Tokens { get; set; } = [with(StringComparer.Ordinal)];

    public sealed record TokenWithId(
        string Id,
        Token Token
    );

    [JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
    [JsonDerivedType(typeof(LevelUpToken), "LEVEL_UP")]
    [JsonDerivedType(typeof(JournalItemUnlockedToken), "JOURNAL_ITEM_UNLOCKED")]
    [JsonDerivedType(typeof(DailyLoginToken), "DAILY_LOGIN")]
    [JsonDerivedType(typeof(ChallengeProgressToken), "CHALLENGE_PROGRESS")]
    public abstract class Token : IEquatable<Token>, ICloneable<Token>
    {
        [JsonIgnore]
        public TypeE Type { get; init; }

        protected Token(TypeE type)
        {
            Type = type;
        }

        [JsonConverter(typeof(JsonStringEnumConverter))]
        public enum TypeE
        {
#pragma warning disable CA1707 // Identifiers should not contain underscores
            LEVEL_UP,
            JOURNAL_ITEM_UNLOCKED,
            DAILY_LOGIN,
            CHALLENGE_PROGRESS
#pragma warning restore CA1707 // Identifiers should not contain underscores
        }

        public abstract bool Equals(Token? other);

        public override bool Equals(object? obj)
            => Equals(obj as Token);

        public abstract override int GetHashCode();

        public abstract Token DeepCopy();

        public sealed class Comparer : IEqualityComparer<Token>
        {
            public static Comparer Instance { get; } = new Comparer();

            private Comparer()
            {
            }

            public bool Equals(Token? x, Token? y)
                => ReferenceEquals(x, y) || (x?.Equals(y) ?? false);

            public int GetHashCode([DisallowNull] Token obj)
                => obj.GetHashCode();
        }
    }

    public sealed class LevelUpToken : Token
    {
        public int Level { get; init; }
        public Rewards Rewards { get; init; }

        public LevelUpToken(int level, Rewards rewards)
            : base(TypeE.LEVEL_UP)
        {
            Level = level;
            Rewards = rewards;
        }

        public override bool Equals(Token? other)
            => other is LevelUpToken levelUp && Level == levelUp.Level && Rewards.Equals(levelUp.Rewards);

        public override int GetHashCode()
            => HashCode.Combine(Level, Rewards);

        public override LevelUpToken DeepCopy()
            => new(Level, Rewards.DeepCopy());
    }

    public sealed class JournalItemUnlockedToken : Token
    {
        public string ItemId { get; init; }

        public JournalItemUnlockedToken(string itemId)
            : base(TypeE.JOURNAL_ITEM_UNLOCKED)
        {
            ItemId = itemId;
        }

        public override bool Equals(Token? other)
            => other is JournalItemUnlockedToken itemUnlocked && ItemId == itemUnlocked.ItemId;

        public override int GetHashCode()
            => HashCode.Combine(ItemId);

        public override JournalItemUnlockedToken DeepCopy()
            => new(ItemId);
    }

    public sealed class DailyLoginToken : Token
    {
        public string Date { get; init; }
        public Rewards Rewards { get; init; }
        public bool Claimed { get; init; }
        public long? ClaimedOn { get; init; }

        public DailyLoginToken(string date, Rewards rewards, bool claimed = false, long? claimedOn = null)
            : base(TypeE.DAILY_LOGIN)
        {
            Date = date;
            Rewards = rewards;
            Claimed = claimed;
            ClaimedOn = claimedOn;
        }

        public override bool Equals(Token? other)
            => other is DailyLoginToken dailyLogin && Date == dailyLogin.Date && Rewards.Equals(dailyLogin.Rewards) && Claimed == dailyLogin.Claimed && ClaimedOn == dailyLogin.ClaimedOn;

        public override int GetHashCode()
            => HashCode.Combine(Date, Rewards, Claimed, ClaimedOn);

        public override DailyLoginToken DeepCopy()
            => new(Date, Rewards.DeepCopy(), Claimed, ClaimedOn);
    }

    public sealed class ChallengeProgressToken : Token
    {
        public long UpdatedAt { get; init; }
        public string? DailyDateUtc { get; init; }
        public string? ActiveSeasonId { get; init; }
        public string? ActiveSeasonChallengeId { get; init; }
        public string? LastDailyLoginDateUtc { get; init; }
        public int DailyLoginStreak { get; init; }
        public int TappablesRedeemed { get; init; }
        public Dictionary<string, int> ObjectiveCounts { get; init; } = [with(StringComparer.Ordinal)];
        public HashSet<string> ClaimedChallengeIds { get; init; } = [with(StringComparer.Ordinal)];
        public HashSet<string> RemovedContinuousChallengeIds { get; init; } = [with(StringComparer.Ordinal)];

        public ChallengeProgressToken() : base(TypeE.CHALLENGE_PROGRESS) { }

        public override bool Equals(Token? other)
            => other is ChallengeProgressToken token &&
            UpdatedAt == token.UpdatedAt &&
            DailyDateUtc == token.DailyDateUtc &&
            ActiveSeasonId == token.ActiveSeasonId &&
            ActiveSeasonChallengeId == token.ActiveSeasonChallengeId &&
            LastDailyLoginDateUtc == token.LastDailyLoginDateUtc &&
            DailyLoginStreak == token.DailyLoginStreak &&
            TappablesRedeemed == token.TappablesRedeemed &&
            ObjectiveCounts.OrderBy(static item => item.Key, StringComparer.Ordinal).SequenceEqual(token.ObjectiveCounts.OrderBy(static item => item.Key, StringComparer.Ordinal)) &&
            ClaimedChallengeIds.Order(StringComparer.Ordinal).SequenceEqual(token.ClaimedChallengeIds.Order(StringComparer.Ordinal), StringComparer.Ordinal) &&
            RemovedContinuousChallengeIds.Order(StringComparer.Ordinal).SequenceEqual(token.RemovedContinuousChallengeIds.Order(StringComparer.Ordinal), StringComparer.Ordinal);

        public override int GetHashCode()
        {
            var hash = new HashCode();
            hash.Add(UpdatedAt);
            hash.Add(DailyDateUtc, StringComparer.Ordinal);
            hash.Add(ActiveSeasonId, StringComparer.Ordinal);
            hash.Add(ActiveSeasonChallengeId, StringComparer.Ordinal);
            hash.Add(LastDailyLoginDateUtc, StringComparer.Ordinal);
            hash.Add(DailyLoginStreak);
            hash.Add(TappablesRedeemed);

            foreach (var item in ObjectiveCounts.OrderBy(static item => item.Key, StringComparer.Ordinal))
            {
                hash.Add(item.Key, StringComparer.Ordinal);
                hash.Add(item.Value);
            }

            foreach (var challengeId in ClaimedChallengeIds.Order(StringComparer.Ordinal))
            {
                hash.Add(challengeId, StringComparer.Ordinal);
            }

            foreach (var challengeId in RemovedContinuousChallengeIds.Order(StringComparer.Ordinal))
            {
                hash.Add(challengeId, StringComparer.Ordinal);
            }

            return hash.ToHashCode();
        }

        public override ChallengeProgressToken DeepCopy() => new()
        {
            UpdatedAt = UpdatedAt,
            DailyDateUtc = DailyDateUtc,
            ActiveSeasonId = ActiveSeasonId,
            ActiveSeasonChallengeId = ActiveSeasonChallengeId,
            TappablesRedeemed = TappablesRedeemed,
            LastDailyLoginDateUtc = LastDailyLoginDateUtc,
            DailyLoginStreak = DailyLoginStreak,
            ObjectiveCounts = ObjectiveCounts.ToDictionary(StringComparer.Ordinal),
            ClaimedChallengeIds = [with(StringComparer.Ordinal), .. ClaimedChallengeIds],
            RemovedContinuousChallengeIds = [with(StringComparer.Ordinal), .. RemovedContinuousChallengeIds]
        };
    }
}
