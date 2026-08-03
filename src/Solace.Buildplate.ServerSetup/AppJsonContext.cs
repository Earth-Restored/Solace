using System.Text.Json.Serialization;

namespace Solace.Buildplate.ServerSetup;

[JsonSourceGenerationOptions(WriteIndented = false, PropertyNameCaseInsensitive = true, PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower)]
[JsonSerializable(typeof(SetupService.ModrinthFile[]))]
[JsonSerializable(typeof(SetupService.ModrinthVersion[]))]
internal sealed partial class AppJsonContext : JsonSerializerContext
{
}

