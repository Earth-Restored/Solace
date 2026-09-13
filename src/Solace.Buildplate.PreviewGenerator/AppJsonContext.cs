using System.Text.Json.Serialization;

namespace Solace.Buildplate.PreviewGenerator;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(PreviewModel))]
internal sealed partial class AppJsonContext : JsonSerializerContext
{
}
