using System.Diagnostics;
using System.Text.Json.Serialization;

namespace Solace.ApiServer.Types.Common;

internal sealed record Token(
    Token.Type ClientType,
    Dictionary<string, string> ClientProperties,
    Rewards Rewards,
    Token.LifetimeE Lifetime
)
{
    [JsonConverter(typeof(JsonStringEnumConverter<Type>))]
    internal enum Type
    {
#pragma warning disable CA1707 // Identifiers should not contain underscores
        [JsonStringEnumMemberName("adv_zyki")]
        LEVEL_UP,
        [JsonStringEnumMemberName("redeemtappable")]
        TAPPABLE,
        [JsonStringEnumMemberName("item.unlocked")]
        JOURNAL_ITEM_UNLOCKED,
        [JsonStringEnumMemberName("daily.login")]
        DAILY_LOGIN,
#pragma warning restore CA1707 // Identifiers should not contain underscores
    }

    [JsonConverter(typeof(JsonStringEnumConverter<LifetimeE>))]
    internal enum LifetimeE
    {
        [JsonStringEnumMemberName("Persistent")]
        PERSISTENT,
        [JsonStringEnumMemberName("Transient")]
        TRANSIENT,
    }
}

#pragma warning disable MA0048 // File name must match type name
internal static class TokenTypeExtensions
#pragma warning restore MA0048 // File name must match type name
{
    extension(Token.Type)
    {
        public static Token.Type FromDb(DB.Earth.Models.Player.TokenEF token)
            => token switch
            {
                DB.Earth.Models.Player.LevelUpTokenEF => Token.Type.LEVEL_UP,  
                DB.Earth.Models.Player.JournalItemUnlockedTokenEF => Token.Type.JOURNAL_ITEM_UNLOCKED,
                DB.Earth.Models.Player.DailyLoginTokenEF => Token.Type.DAILY_LOGIN,
                _ => throw new UnreachableException(),
            };
    }
}
