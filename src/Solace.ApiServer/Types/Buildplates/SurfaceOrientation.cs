using System.Text.Json.Serialization;

namespace Solace.ApiServer.Types.Buildplates;

[JsonConverter(typeof(JsonStringEnumConverter<SurfaceOrientation>))]
internal enum SurfaceOrientation
{
    [JsonStringEnumMemberName("Horizontal")] HORIZONTAL,
    [JsonStringEnumMemberName("Vertical")] VERTICAL,   // TODO: unverified
}
