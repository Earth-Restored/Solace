using System.Text.Json.Serialization;

namespace Solace.WebPortal.Common.Features.Players;

[JsonConverter(typeof(JsonStringEnumConverter<SkinType>))]
public enum SkinType
{
    Auto,
    Wide,
    Slim,
}
