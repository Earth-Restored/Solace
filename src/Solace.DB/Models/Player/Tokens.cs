using System.Text.Json.Serialization;
using Solace.Common.Utils;
using Solace.DB.Models.Common;

namespace Solace.DB.Models.Player;

public sealed class Tokens
{
    [JsonInclude, JsonPropertyName("tokens")]
    public Dictionary<string, Token> _tokens;

    public Tokens()
    {
        _tokens = [];
    }

    public Tokens Copy()
    {
        var tokens = new Tokens();
        tokens._tokens.AddRange(_tokens);
        return tokens;
    }

    public sealed record TokenWithId(
        string Id,
        Token Token
    );

    public TokenWithId[] GetTokens()
        => [.. _tokens.Select(item => new TokenWithId(item.Key, item.Value))];

    public void AddToken(string id, Token token)
        => _tokens[id] = token;

    public Token? RemoveToken(string id)
    {
        Token? res = null;
        if (_tokens.TryGetValue(id, out Token? t))
        {
            res = t;
        }

        _tokens.Remove(id);

        return res;
    }

    [JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
    [JsonDerivedType(typeof(LevelUpToken), "LEVEL_UP")]
    [JsonDerivedType(typeof(JournalItemUnlockedToken), "JOURNAL_ITEM_UNLOCKED")]
    [JsonDerivedType(typeof(DailyLoginToken), "DAILY_LOGIN")]
    [JsonDerivedType(typeof(OobeAdventureCrystalToken), "OOBE_ADVENTURE_CRYSTAL")]
    [JsonDerivedType(typeof(ChallengeProgressToken), "CHALLENGE_PROGRESS")]
    [JsonDerivedType(typeof(ChallengeCompletedToken), "CHALLENGE_COMPLETED")]
    public abstract class Token
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
            OOBE_ADVENTURE_CRYSTAL,
            CHALLENGE_PROGRESS,
            CHALLENGE_COMPLETED
#pragma warning restore CA1707 // Identifiers should not contain underscores
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
    }

    public sealed class JournalItemUnlockedToken : Token
    {
        public string ItemId { get; init; }

        public JournalItemUnlockedToken(string itemId)
            : base(TypeE.JOURNAL_ITEM_UNLOCKED)
        {
            ItemId = itemId;
        }
    }

    public sealed class DailyLoginToken : Token
    {
        public string Date { get; init; }
        public Rewards Rewards { get; init; }

        public DailyLoginToken(string date, Rewards rewards)
            : base(TypeE.DAILY_LOGIN)
        {
            Date = date;
            Rewards = rewards;
        }
    }

    public sealed class OobeAdventureCrystalToken : Token
    {
        public string ItemId { get; init; }
        public Rewards Rewards { get; init; }

        public OobeAdventureCrystalToken(string itemId, Rewards rewards)
            : base(TypeE.OOBE_ADVENTURE_CRYSTAL)
        {
            ItemId = itemId;
            Rewards = rewards;
        }
    }

    public sealed class ChallengeProgressToken : Token
    {
        public string ChallengeId { get; init; }
        public string ChallengeReferenceId { get; init; }

        public ChallengeProgressToken(string challengeId, string challengeReferenceId)
            : base(TypeE.CHALLENGE_PROGRESS)
        {
            ChallengeId = challengeId;
            ChallengeReferenceId = challengeReferenceId;
        }
    }

    public sealed class ChallengeCompletedToken : Token
    {
        public string ChallengeId { get; init; }
        public string ChallengeReferenceId { get; init; }

        public ChallengeCompletedToken(string challengeId, string challengeReferenceId)
            : base(TypeE.CHALLENGE_COMPLETED)
        {
            ChallengeId = challengeId;
            ChallengeReferenceId = challengeReferenceId;
        }
    }
}
