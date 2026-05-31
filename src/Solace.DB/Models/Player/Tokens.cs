using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
using Solace.Common;
using Solace.Common.Utils;
using Solace.DB.Models.Common;

namespace Solace.DB.Models.Player;

public sealed class TokensEF : IEntityWithId<Guid>, IVersionedEntity, IMergeable<TokensEF>
{
    public Guid Id { get; set; }

    public int Version { get; set; } = 1;

    public Account Account { get; set; } = null!;

    public Dictionary<string, Token> Tokens { get; set; } = [];

    public sealed record TokenWithId(
        string Id,
        Token Token
    );

    public TokenWithId[] GetTokens()
        => [.. Tokens.Select(item => new TokenWithId(item.Key, item.Value))];

    public void AddToken(string id, Token token)
        => Tokens[id] = token;

    public Token? RemoveToken(string id)
    {
        Tokens.Remove(id, out var token);

        return token;
    }

    public async Task MergeWith(TokensEF other, ValueMerger merger)
    {
        merger.CurrentUserId = Id.ToString();
        merger.CurrentUsername = Account?.Username;

        foreach (var item in other.Tokens)
        {
            Tokens[item.Key] = item.Value;
        }
    }

    [JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
    [JsonDerivedType(typeof(LevelUpToken), "LEVEL_UP")]
    [JsonDerivedType(typeof(JournalItemUnlockedToken), "JOURNAL_ITEM_UNLOCKED")]
    [JsonDerivedType(typeof(DailyLoginToken), "DAILY_LOGIN")]
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
            DAILY_LOGIN
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
                => x == y || (x?.Equals(y) ?? false);

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
            => new LevelUpToken(Level, Rewards.DeepCopy());
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
            => new JournalItemUnlockedToken(ItemId);
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
            => new DailyLoginToken(Date, Rewards.DeepCopy(), Claimed, ClaimedOn);
    }

    public sealed class Legacy : IEquatable<Legacy>
    {
        [JsonInclude, JsonPropertyName("tokens")]
        public Dictionary<string, Token> Tokens;

        public Legacy()
        {
            Tokens = [];
        }

        public sealed record TokenWithId(
            string Id,
            Token Token
        );

        public bool Equals(Legacy? other)
            => other is not null && Tokens.OrderBy(static item => item.Key, StringComparer.Ordinal).Select(item => (Key: item.Key, Value: item.Value)).SequenceEqual(other.Tokens.OrderBy(static item => item.Key, StringComparer.Ordinal).Select(item => (Key: item.Key, Value: item.Value)));

        public override bool Equals(object? obj)
            => Equals(obj as Legacy);

        public override int GetHashCode()
        {
            var hash = new HashCode();

            foreach (var item in Tokens.OrderBy(static item => item.Key, StringComparer.Ordinal))
            {
                hash.Add(item.Key);
                hash.Add(item.Value);
            }

            return hash.ToHashCode();
        }

        [JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
        [JsonDerivedType(typeof(LevelUpToken), "LEVEL_UP")]
        [JsonDerivedType(typeof(JournalItemUnlockedToken), "JOURNAL_ITEM_UNLOCKED")]
        public abstract class Token : IEquatable<Token>
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
                JOURNAL_ITEM_UNLOCKED
#pragma warning restore CA1707 // Identifiers should not contain underscores
            }

            public abstract bool Equals(Token? other);

            public override bool Equals(object? obj)
                => Equals(obj as Token);

            public abstract override int GetHashCode();
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
        }
    }
}
