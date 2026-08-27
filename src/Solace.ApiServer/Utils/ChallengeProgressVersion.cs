using System.Globalization;
using Solace.DB.Models.Player;

namespace Solace.ApiServer.Utils;

public sealed class ChallengeProgressVersion
{
    public long UpdatedAt { get; set; }
    public string? DailyDateUtc { get; set; }
    public string? ActiveSeasonId { get; set; }
    public string? ActiveSeasonChallengeId { get; set; }
    public string? LastDailyLoginDateUtc { get; set; }
    public int DailyLoginStreak { get; set; }
    public int TappablesRedeemed { get; set; }
    public Dictionary<string, int> ObjectiveCounts { get; set; } = [];
    public HashSet<string> ClaimedChallengeIds { get; set; } = [];
    public HashSet<string> RemovedContinuousChallengeIds { get; set; } = [];

    public void EnsureDate(long timestamp)
    {
        string today = DateTimeOffset.FromUnixTimeMilliseconds(timestamp)
            .UtcDateTime
            .ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);

        ObjectiveCounts ??= [];
        ClaimedChallengeIds ??= [];
        RemovedContinuousChallengeIds ??= [];

        if (DailyDateUtc == today)
        {
            return;
        }

        DailyDateUtc = today;
        TappablesRedeemed = 0;
        ObjectiveCounts = [];
        RemovedContinuousChallengeIds = [];
    }

    public int RecordTappable(long timestamp)
    {
        EnsureDate(timestamp);
        UpdatedAt = timestamp;
        TappablesRedeemed++;
        return TappablesRedeemed;
    }

    public void AddObjectiveProgress(long timestamp, string objectiveId, int amount = 1)
    {
        EnsureDate(timestamp);
        UpdatedAt = timestamp;
        ObjectiveCounts ??= [];
        ObjectiveCounts[objectiveId] = ObjectiveCounts.GetValueOrDefault(objectiveId) + amount;
    }

    public int GetObjectiveProgress(string objectiveId)
    {
        ObjectiveCounts ??= [];
        return ObjectiveCounts.GetValueOrDefault(objectiveId);
    }

    public int GetDailyLoginDay(long timestamp)
    {
        string today = Date(timestamp);
        int streak = LastDailyLoginDateUtc switch
        {
            null => 1,
            var date when date == today => Math.Max(1, DailyLoginStreak),
            var date when date == Date(timestamp - TimeSpan.FromDays(1).Ticks / TimeSpan.TicksPerMillisecond) => Math.Max(1, DailyLoginStreak) + 1,
            _ => 1,
        };

        return (streak - 1) % 7 + 1;
    }

    public bool IsDailyLoginClaimed(long timestamp)
        => LastDailyLoginDateUtc == Date(timestamp);

    public void ClaimDailyLogin(long timestamp)
    {
        string today = Date(timestamp);
        if (LastDailyLoginDateUtc == today)
        {
            return;
        }

        string yesterday = Date(timestamp - TimeSpan.FromDays(1).Ticks / TimeSpan.TicksPerMillisecond);
        DailyLoginStreak = LastDailyLoginDateUtc == yesterday ? Math.Max(1, DailyLoginStreak) + 1 : 1;
        LastDailyLoginDateUtc = today;
        UpdatedAt = timestamp;
    }

    public static ChallengeProgressVersion FromToken(TokensEF.ChallengeProgressToken token)
        => new()
        {
            UpdatedAt = token.UpdatedAt,
            DailyDateUtc = token.DailyDateUtc,
            ActiveSeasonId = token.ActiveSeasonId,
            ActiveSeasonChallengeId = token.ActiveSeasonChallengeId,
            LastDailyLoginDateUtc = token.LastDailyLoginDateUtc,
            DailyLoginStreak = token.DailyLoginStreak,
            TappablesRedeemed = token.TappablesRedeemed,
            ObjectiveCounts = new(token.ObjectiveCounts),
            ClaimedChallengeIds = [.. token.ClaimedChallengeIds],
            RemovedContinuousChallengeIds = [.. token.RemovedContinuousChallengeIds],
        };

    public TokensEF.ChallengeProgressToken ToToken()
        => new()
        {
            UpdatedAt = UpdatedAt,
            DailyDateUtc = DailyDateUtc,
            ActiveSeasonId = ActiveSeasonId,
            ActiveSeasonChallengeId = ActiveSeasonChallengeId,
            LastDailyLoginDateUtc = LastDailyLoginDateUtc,
            DailyLoginStreak = DailyLoginStreak,
            TappablesRedeemed = TappablesRedeemed,
            ObjectiveCounts = new(ObjectiveCounts),
            ClaimedChallengeIds = [.. ClaimedChallengeIds],
            RemovedContinuousChallengeIds = [.. RemovedContinuousChallengeIds],
        };

    private static string Date(long timestamp)
        => DateTimeOffset.FromUnixTimeMilliseconds(timestamp).UtcDateTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}
