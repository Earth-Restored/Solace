using System.Text.Json.Serialization;

namespace Solace.ApiServer.Types.Tappables;

[JsonConverter(typeof(JsonStringEnumConverter<ActiveLocationType>))]
internal enum ActiveLocationType
{
#pragma warning disable CA1707 // Identifiers should not contain underscores
    [JsonStringEnumMemberName("Tappable")] TAPPABLE,
    [JsonStringEnumMemberName("Encounter")] ENCOUNTER,
    [JsonStringEnumMemberName("PlayerAdventure")] PLAYER_ADVENTURE,
#pragma warning restore CA1707 // Identifiers should not contain underscores
}
