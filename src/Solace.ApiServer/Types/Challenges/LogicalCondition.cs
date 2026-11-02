using System.ComponentModel;
using System.Text.Json.Serialization;
using Solace.Db.Earth.Models.Challenges;

namespace Solace.ApiServer.Types.Challenges;

[JsonConverter(typeof(JsonStringEnumConverter<LogicalCondition>))]
internal enum LogicalCondition
{
    And = 1,
    Or = 2,
}

#pragma warning disable MA0048 // File name must match type name
internal static class LogicalConditionExtensions
#pragma warning restore MA0048 // File name must match type name
{
    extension(LogicalCondition)
    {
        public static LogicalCondition FromDb(LogicalConditionEF condition)
            => condition switch
            {
                LogicalConditionEF.And => LogicalCondition.And,
                LogicalConditionEF.Or => LogicalCondition.Or,
                _ => throw new InvalidEnumArgumentException(nameof(condition), (int)condition, typeof(LogicalConditionEF)),
            };

        public static LogicalCondition FromStaticData(Solace.StaticData.LogicalCondition condition)
            => condition switch
            {
                Solace.StaticData.LogicalCondition.And => LogicalCondition.And,
                Solace.StaticData.LogicalCondition.Or => LogicalCondition.Or,
                _ => throw new InvalidEnumArgumentException(nameof(condition), (int)condition, typeof(Solace.StaticData.LogicalCondition)),
            };
    }

    extension(LogicalCondition condition)
    {
        public LogicalConditionEF ToDb()
            => condition switch
            {
                LogicalCondition.And => LogicalConditionEF.And,
                LogicalCondition.Or => LogicalConditionEF.Or,
                _ => throw new InvalidEnumArgumentException(nameof(condition), (int)condition, typeof(LogicalCondition)),
            };
    }
}
