using System.Text.Json.Serialization;

namespace Solace.Db.Playfab.Models.Items;

[JsonConverter(typeof(JsonStringEnumConverter<RarityEF>))]
public enum RarityEF
{
    None,
    Common,
    Uncommon,
    Rare,
    Epic,
    Legendary,
}
