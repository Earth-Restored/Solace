using System.Text.Json.Serialization;

namespace Solace.Db.Playfab.Models.Tabs;

[JsonConverter(typeof(JsonStringEnumConverter<ColumnTypeEF>))]
public enum ColumnTypeEF
{
    Rectangle,
    Square,
    Grid,
}
