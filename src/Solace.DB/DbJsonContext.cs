using System.Text.Json.Serialization;

namespace Solace.DB;

[JsonSourceGenerationOptions]
[JsonSerializable(typeof(Dictionary<Guid, int>))]
internal sealed partial class DbJsonContext : JsonSerializerContext
{
}