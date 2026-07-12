using System.Text.Json.Serialization;

namespace Solace.ApiServer.Types.Tappables;

internal sealed record EncounterState(
    EncounterState.ActiveEncounterStateE ActiveEncounterState
)
{
    [JsonConverter(typeof(JsonStringEnumConverter<ActiveEncounterStateE>))]
    internal enum ActiveEncounterStateE
    {
        [JsonStringEnumMemberName("Pristine")] PRISTINE,
        [JsonStringEnumMemberName("Dirty")] DIRTY,
    }
}