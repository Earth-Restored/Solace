using System.Text.Json.Serialization;

namespace Solace.TileRenderer;

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    PropertyNameCaseInsensitive = true
)]
[JsonSerializable(typeof(MaptilerTileDataSource.TilesResponse))]
[JsonSerializable(typeof(RenderTileRequest))]
internal sealed partial class AppJsonContext : JsonSerializerContext
{
}