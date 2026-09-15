using System.Text.Json.Serialization;

namespace Solace.ApiServer.Types.Common;

[JsonConverter(typeof(JsonStringEnumConverter<TokenType>))]
internal enum TokenType
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
