
using System.ComponentModel;
using System.Text.Json.Serialization;

namespace Solace.ApiServer.Types.Common;

public sealed record Token(
    Token.Type ClientType,
    Dictionary<string, string> ClientProperties,
    Rewards Rewards,
    Token.LifetimeE Lifetime
)
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum Type
    {
#pragma warning disable CA1707 // Identifiers should not contain underscores
        [JsonStringEnumMemberName("adv_zyki")]
        LEVEL_UP,
        [JsonStringEnumMemberName("redeemtappable")]
        TAPPABLE,
        [JsonStringEnumMemberName("item.unlocked")]
        JOURNAL_ITEM_UNLOCKED,
        [JsonStringEnumMemberName("daily.login")]
        DAILY_LOGIN
#pragma warning restore CA1707 // Identifiers should not contain underscores
    }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum LifetimeE
    {
        [JsonStringEnumMemberName("Persistent")]
        PERSISTENT,
        [JsonStringEnumMemberName("Transient")]
        TRANSIENT
    }
}

public static class TokenTypeExtensions
{
    extension(Token.Type)
    {
        public static Token.Type FromDb(DB.Models.Player.TokensEF.Token.TypeE type)
            => type switch
            {
                DB.Models.Player.TokensEF.Token.TypeE.LEVEL_UP => Token.Type.LEVEL_UP,  
                DB.Models.Player.TokensEF.Token.TypeE.JOURNAL_ITEM_UNLOCKED => Token.Type.JOURNAL_ITEM_UNLOCKED,
                DB.Models.Player.TokensEF.Token.TypeE.DAILY_LOGIN => Token.Type.DAILY_LOGIN,
                _ => throw new InvalidEnumArgumentException(nameof(type), (int)type, typeof(DB.Models.Player.TokensEF.Token.TypeE)),  
            };
    }
}
