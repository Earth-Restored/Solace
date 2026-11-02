using System.Text.Json.Serialization;

namespace Solace.StaticData;

[JsonConverter(typeof(JsonStringEnumConverter<ObjectiveType>))]
public enum ObjectiveType
{
    TappableCollected = 1,
    CraftItem = 2,
    SmeltItem = 3,
    AdventurePlayed = 4,
    DefeatMob = 5,
    SeasonCapstone = 6, // needs personaItems reward
    PlaceBlock = 7,
}
