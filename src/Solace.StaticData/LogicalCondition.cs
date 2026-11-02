using System.Text.Json.Serialization;

namespace Solace.StaticData;

[JsonConverter(typeof(JsonStringEnumConverter<LogicalCondition>))]
public enum LogicalCondition
{
    And = 1,
    Or = 2,
}
