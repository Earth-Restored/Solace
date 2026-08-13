using System.Text.Json.Serialization;

namespace Solace.Db.Playfab.Models.Items;

[JsonConverter(typeof(JsonStringEnumConverter<BuildplateSizeEF>))]
public enum BuildplateSizeEF
{
    Small,
    Medium,
    Large,
}
