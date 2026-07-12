using System.Text.Json.Serialization;

namespace Solace.TappablesGenerator;

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    PropertyNameCaseInsensitive = true
)]
[JsonSerializable(typeof(ActiveTiles.ActiveTileNotification))]
[JsonSerializable(typeof(Encounter))]
[JsonSerializable(typeof(Tappable))]
internal sealed partial class AppJsonContext : JsonSerializerContext
{
}