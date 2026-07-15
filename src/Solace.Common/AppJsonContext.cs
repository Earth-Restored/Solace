using System.Text.Json.Serialization;
using Solace.Buildplate.Model;

namespace Solace.Common;

[JsonSourceGenerationOptions(WriteIndented = false, PropertyNameCaseInsensitive = true, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(BuildplateMetadataVersion))]
[JsonSerializable(typeof(BuildplateMetadataV1))]
internal sealed partial class AppJsonContext : JsonSerializerContext
{
}