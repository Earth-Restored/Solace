using System.Text.Json.Serialization;

namespace Solace.Db.Playfab.Models.Tabs;

[JsonConverter(typeof(JsonStringEnumConverter<ContentTypeEF>))]
public enum ContentTypeEF
{
    Durable,
    Collection,
    Bundle,
    Persona,
    Genoa,
    BuildplateOffer,
    RubyOffer,
    InventoryItemOffer,
}
