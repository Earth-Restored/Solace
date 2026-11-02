using System.Text.Json;
using System.Text.Json.Serialization;

namespace Solace.ApiServer.Types.Challenges;

[JsonConverter(typeof(JsonStringEnumConverter<ChallengeType>))]
internal enum ChallengeType
{
    Regular = 1,
    Journal = 2,
}
