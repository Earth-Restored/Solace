namespace Solace.ApiServer.Utils;

public sealed class ChallengeProgressVersion
{
    public long UpdatedAt { get; set; }
    public string? DailyDateUtc { get; set; }
    public string? ActiveSeasonId { get; set; }
    public string? ActiveSeasonChallengeId { get; set; }
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
}
