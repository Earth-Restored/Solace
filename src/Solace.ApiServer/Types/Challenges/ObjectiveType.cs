using System.ComponentModel;
using System.Text.Json.Serialization;
using Solace.Db.Earth.Models.Challenges;

namespace Solace.ApiServer.Types.Challenges;

[JsonConverter(typeof(JsonStringEnumConverter<ObjectiveType>))]
internal enum ObjectiveType
{
    TappableCollected = 1,
    CraftItem = 2,
    SmeltItem = 3,
    AdventurePlayed = 4,
    DefeatMob = 5,
    SeasonCapstone = 6,
    PlaceBlock = 7,
}

#pragma warning disable MA0048 // File name must match type name
internal static class ObjectiveTypeExtensions
#pragma warning restore MA0048 // File name must match type name
{
    extension(ObjectiveType)
    {
        public static ObjectiveType FromDb(ObjectiveTypeEF type)
            => type switch
            {
                ObjectiveTypeEF.TappableCollected => ObjectiveType.TappableCollected,
                ObjectiveTypeEF.CraftItem => ObjectiveType.CraftItem,
                ObjectiveTypeEF.SmeltItem => ObjectiveType.SmeltItem,
                ObjectiveTypeEF.AdventurePlayed => ObjectiveType.AdventurePlayed,
                ObjectiveTypeEF.DefeatMob => ObjectiveType.DefeatMob,
                ObjectiveTypeEF.SeasonCapstone => ObjectiveType.SeasonCapstone,
                ObjectiveTypeEF.PlaceBlock => ObjectiveType.PlaceBlock,
                _ => throw new InvalidEnumArgumentException(nameof(type), (int)type, typeof(ObjectiveTypeEF)),
            };

        public static ObjectiveType FromStaticData(Solace.StaticData.ObjectiveType type)
            => type switch
            {
                Solace.StaticData.ObjectiveType.TappableCollected => ObjectiveType.TappableCollected,
                Solace.StaticData.ObjectiveType.CraftItem => ObjectiveType.CraftItem,
                Solace.StaticData.ObjectiveType.SmeltItem => ObjectiveType.SmeltItem,
                Solace.StaticData.ObjectiveType.AdventurePlayed => ObjectiveType.AdventurePlayed,
                Solace.StaticData.ObjectiveType.DefeatMob => ObjectiveType.DefeatMob,
                Solace.StaticData.ObjectiveType.SeasonCapstone => ObjectiveType.SeasonCapstone,
                Solace.StaticData.ObjectiveType.PlaceBlock => ObjectiveType.PlaceBlock,
                _ => throw new InvalidEnumArgumentException(nameof(type), (int)type, typeof(Solace.StaticData.ObjectiveType)),
            };
    }

    extension(ObjectiveType type)
    {
        public ObjectiveTypeEF ToDb()
            => type switch
            {
                ObjectiveType.TappableCollected => ObjectiveTypeEF.TappableCollected,
                ObjectiveType.CraftItem => ObjectiveTypeEF.CraftItem,
                ObjectiveType.SmeltItem => ObjectiveTypeEF.SmeltItem,
                ObjectiveType.AdventurePlayed => ObjectiveTypeEF.AdventurePlayed,
                ObjectiveType.DefeatMob => ObjectiveTypeEF.DefeatMob,
                ObjectiveType.SeasonCapstone => ObjectiveTypeEF.SeasonCapstone,
                ObjectiveType.PlaceBlock => ObjectiveTypeEF.PlaceBlock,
                _ => throw new InvalidEnumArgumentException(nameof(type), (int)type, typeof(ObjectiveType)),
            };
    }
}
