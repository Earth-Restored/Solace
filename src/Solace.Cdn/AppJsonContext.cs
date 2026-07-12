using System.Text.Json.Serialization;
using Solace.Cdn.Utils;

namespace Solace.Cdn;

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    PropertyNameCaseInsensitive = true
)]
[JsonSerializable(typeof(TileUtils.RenderTileRequest))]
internal sealed partial class AppJsonContext : JsonSerializerContext
{
}