using System.ComponentModel;
using System.Text.Json.Serialization;
using Solace.Db.Earth.Models.Challenges;

namespace Solace.ApiServer.Types.Challenges;

[JsonConverter(typeof(JsonStringEnumConverter<ChallengeDuration>))]
internal enum ChallengeDuration
{
    Daily = 1,
    PersonalTimed = 5,
    PersonalContinuous = 6,
    Season = 7,
    SignIn = 8,
    OOBE = 9,
}

#pragma warning disable MA0048 // File name must match type name
internal static class ChallengeDurationExtensions
#pragma warning restore MA0048 // File name must match type name
{
    extension(ChallengeDuration)
    {
        public static ChallengeDuration FromDb(ChallengeDurationEF duration)
            => duration switch
            {
                ChallengeDurationEF.Daily => ChallengeDuration.Daily,
                ChallengeDurationEF.PersonalTimed => ChallengeDuration.PersonalTimed,
                ChallengeDurationEF.PersonalContinuous => ChallengeDuration.PersonalContinuous,
                ChallengeDurationEF.Season => ChallengeDuration.Season,
                ChallengeDurationEF.SignIn => ChallengeDuration.SignIn,
                ChallengeDurationEF.OOBE => ChallengeDuration.OOBE,
                _ => throw new InvalidEnumArgumentException(nameof(duration), (int)duration, typeof(ChallengeDurationEF)),
            };
    }

    extension(ChallengeDuration duration)
    {
        public ChallengeDurationEF ToDb()
            => duration switch
            {
                ChallengeDuration.Daily => ChallengeDurationEF.Daily,
                ChallengeDuration.PersonalTimed => ChallengeDurationEF.PersonalTimed,
                ChallengeDuration.PersonalContinuous => ChallengeDurationEF.PersonalContinuous,
                ChallengeDuration.Season => ChallengeDurationEF.Season,
                ChallengeDuration.SignIn => ChallengeDurationEF.SignIn,
                ChallengeDuration.OOBE => ChallengeDurationEF.OOBE,
                _ => throw new InvalidEnumArgumentException(nameof(duration), (int)duration, typeof(ChallengeDuration)),
            };
    }

}
