using System.ComponentModel;
using System.Text.Json.Serialization;
using Solace.Db.Earth.Models.Challenges;

namespace Solace.ApiServer.Types.Challenges;

[JsonConverter(typeof(JsonStringEnumConverter<ChallengeState>))]
internal enum ChallengeState
{
    Active = 1,
    Locked = 2,
    Completed = 3,
}

#pragma warning disable MA0048 // File name must match type name
internal static class ChallengeStateExtensions
#pragma warning restore MA0048 // File name must match type name
{
    extension(ChallengeState)
    {
        public static ChallengeState FromDb(ChallengeStateEF state)
            => state switch
            {
                ChallengeStateEF.Active => ChallengeState.Active,
                ChallengeStateEF.Locked => ChallengeState.Locked,
                ChallengeStateEF.Completed => ChallengeState.Completed,
                _ => throw new InvalidEnumArgumentException(nameof(state), (int)state, typeof(ChallengeStateEF)),
            };
    }

    extension(ChallengeState state)
    {
        public ChallengeStateEF ToDb()
            => state switch
            {
                ChallengeState.Active => ChallengeStateEF.Active,
                ChallengeState.Locked => ChallengeStateEF.Locked,
                ChallengeState.Completed => ChallengeStateEF.Completed,
                _ => throw new InvalidEnumArgumentException(nameof(state), (int)state, typeof(ChallengeState)),
            };
    }
}
