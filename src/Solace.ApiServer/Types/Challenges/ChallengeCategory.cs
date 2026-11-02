using System.ComponentModel;
using System.Text.Json.Serialization;
using Solace.Db.Earth.Models.Challenges;

namespace Solace.ApiServer.Types.Challenges;

[JsonConverter(typeof(JsonStringEnumConverter<ChallengeCategory>))]
internal enum ChallengeCategory
{
    Daily = 1,
    Season = 3,
    Activity = 4,
    Journal = 5,
}

#pragma warning disable MA0048 // File name must match type name
internal static class ChallengeCategoryExtensions
#pragma warning restore MA0048 // File name must match type name
{
    extension(ChallengeCategory)
    {
        public static ChallengeCategory FromDb(ChallengeCategoryEF category)
            => category switch
            {
                ChallengeCategoryEF.Daily => ChallengeCategory.Daily,
                ChallengeCategoryEF.Season => ChallengeCategory.Season,
                ChallengeCategoryEF.Activity => ChallengeCategory.Activity,
                ChallengeCategoryEF.Journal => ChallengeCategory.Journal,
                _ => throw new InvalidEnumArgumentException(nameof(category), (int)category, typeof(ChallengeCategoryEF)),
            };
    }

    extension(ChallengeCategory category)
    {
        public ChallengeCategoryEF ToDb()
            => category switch
            {
                ChallengeCategory.Daily => ChallengeCategoryEF.Daily,
                ChallengeCategory.Season => ChallengeCategoryEF.Season,
                ChallengeCategory.Activity => ChallengeCategoryEF.Activity,
                ChallengeCategory.Journal => ChallengeCategoryEF.Journal,
                _ => throw new InvalidEnumArgumentException(nameof(category), (int)category, typeof(ChallengeCategory)),
            };
    }

}
