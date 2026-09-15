using System.Text.Json.Serialization;

namespace Solace.ApiServer.Types.Buildplates;

[JsonConverter(typeof(JsonStringEnumConverter<SharedBuildplateDataType>))]
internal enum SharedBuildplateDataType
{
    [JsonStringEnumMemberName("Survival")] SURVIVAL,
}
