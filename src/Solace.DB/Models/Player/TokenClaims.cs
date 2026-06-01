using System.Text.Json.Serialization;

namespace Solace.DB.Models.Player;

public sealed class TokenClaims
{
    [JsonInclude]
    public string? LastDailyLoginDate { get; set; }

    [JsonInclude]
    public int DailyLoginStreak { get; set; }

    [JsonInclude]
    public bool OobeAdventureCrystalGranted { get; set; }

    [JsonInclude]
    public bool OobeAdventureCrystalRedeemed { get; set; }

    [JsonInclude]
    public HashSet<string> RedeemedDailyLoginDates { get; set; } = [];

    [JsonInclude]
    public HashSet<string> RedeemedChallengeRewardKeys { get; set; } = [];
}
