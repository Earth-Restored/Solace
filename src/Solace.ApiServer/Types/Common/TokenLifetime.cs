using System.Text.Json.Serialization;

namespace Solace.ApiServer.Types.Common;

[JsonConverter(typeof(JsonStringEnumConverter<TokenLifetime>))]
internal enum TokenLifetime
{
    [JsonStringEnumMemberName("Persistent")]
    PERSISTENT,
    [JsonStringEnumMemberName("Transient")]
    TRANSIENT,
}
