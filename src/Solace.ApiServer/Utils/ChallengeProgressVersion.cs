namespace Solace.ApiServer.Utils;

internal sealed class ChallengeProgressVersion
{
    public DateTimeOffset UpdatedAt { get; set; }
    public DateOnly? DailyDateUtc { get; set; }
    public Guid? ActiveSeasonId { get; set; }
    public Guid? ActiveSeasonChallengeId { get; set; }
    public DateOnly? LastDailyLoginDateUtc { get; set; }
    public int DailyLoginStreak { get; set; }
    public int TappablesRedeemed { get; set; }
    public Dictionary<Guid, int> ObjectiveCounts { get; set; } = [];
    public HashSet<Guid> ClaimedChallengeIds { get; set; } = [];
    public HashSet<Guid> RemovedContinuousChallengeIds { get; set; } = [];

    public void EnsureDate(DateTimeOffset timestamp)
    {
        var today = Date(timestamp);

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

    public int RecordTappable(DateTimeOffset timestamp)
    {
        EnsureDate(timestamp);
        UpdatedAt = timestamp;
        TappablesRedeemed++;
        return TappablesRedeemed;
    }

    public void AddObjectiveProgress(DateTimeOffset timestamp, Guid objectiveId, int amount = 1)
    {
        EnsureDate(timestamp);
        UpdatedAt = timestamp;
        ObjectiveCounts ??= [];
        ObjectiveCounts[objectiveId] = ObjectiveCounts.GetValueOrDefault(objectiveId) + amount;
    }

    public int GetObjectiveProgress(Guid objectiveId)
    {
        ObjectiveCounts ??= [];
        return ObjectiveCounts.GetValueOrDefault(objectiveId);
    }

    public int GetDailyLoginDay(DateTimeOffset timestamp)
    {
        var today = Date(timestamp);
        var streak = LastDailyLoginDateUtc switch
        {
            null => 1,
            var date when date == today => int.Max(1, DailyLoginStreak),
            var date when date == Date(timestamp - TimeSpan.FromDays(1)) => int.Max(1, DailyLoginStreak) + 1,
            _ => 1,
        };

        return (streak - 1) % 7 + 1;
    }

    public bool IsDailyLoginClaimed(DateTimeOffset timestamp)
        => LastDailyLoginDateUtc == Date(timestamp);

    public void ClaimDailyLogin(DateTimeOffset timestamp)
    {
        var today = Date(timestamp);
        if (LastDailyLoginDateUtc == today)
        {
            return;
        }

        var yesterday = Date(timestamp - TimeSpan.FromDays(1));
        DailyLoginStreak = LastDailyLoginDateUtc == yesterday ? int.Max(1, DailyLoginStreak) + 1 : 1;
        LastDailyLoginDateUtc = today;
        UpdatedAt = timestamp;
    }

    private static DateOnly Date(DateTimeOffset timestamp)
        => DateOnly.FromDateTime(timestamp.UtcDateTime);
}
