using System.Text.Json.Serialization;

namespace Solace.TappablesGenerator;

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    PropertyNameCaseInsensitive = true
)]
[JsonSerializable(typeof(ActiveTiles.ActiveTileNotification))]
[JsonSerializable(typeof(List<Encounter>))]
[JsonSerializable(typeof(List<Tappable>))]
internal sealed partial class AppJsonContext : JsonSerializerContext
{
}