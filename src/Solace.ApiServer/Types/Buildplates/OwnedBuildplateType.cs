using System.Text.Json.Serialization;

namespace Solace.ApiServer.Types.Buildplates;

[JsonConverter(typeof(JsonStringEnumConverter<OwnedBuildplateType>))]
internal enum OwnedBuildplateType
{
    [JsonStringEnumMemberName("Survival")] SURVIVAL,
}
