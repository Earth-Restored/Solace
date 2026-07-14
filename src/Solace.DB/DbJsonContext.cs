using System.Text.Json.Serialization;
using Solace.DB.Models.Common;

namespace Solace.DB;

[JsonSourceGenerationOptions]
[JsonSerializable(typeof(Dictionary<Guid, int>))]
[JsonSerializable(typeof(Rewards))]
internal sealed partial class DbJsonContext : JsonSerializerContext
{
}