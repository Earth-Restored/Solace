using System.Reflection;
using System.Text.Json;
using Solace.ApiServer.Utils;
using Solace.DB;
using Solace.DB.Models.Player;
using DBRewards = Solace.DB.Models.Common.Rewards;

namespace DailyLoginCycleCheck;

internal static class Program
{
    private static readonly DateTimeOffset FirstDay = new(2026, 8, 9, 0, 0, 0, TimeSpan.Zero);
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private static void Main()
    {
        var progress = new ChallengeProgressVersion();
        Expect(progress.GetDailyLoginDay(Day(0)) == 1, "new players start at day 1");
        progress.ClaimDailyLogin(Day(0));
        Expect(progress.IsDailyLoginClaimed(Day(0)), "today is claimed");
        progress.ClaimDailyLogin(Day(0));
        Expect(progress.DailyLoginStreak == 1, "duplicate claims are idempotent");

        for (int offset = 1; offset < 7; offset++)
        {
            Expect(progress.GetDailyLoginDay(Day(offset)) == offset + 1, $"day {offset + 1} is next");
            progress.ClaimDailyLogin(Day(offset));
        }

        Expect(progress.GetDailyLoginDay(Day(7)) == 1, "day 8 wraps to day 1");
        progress.ClaimDailyLogin(Day(9));
        Expect(progress.DailyLoginStreak == 1 && progress.GetDailyLoginDay(Day(9)) == 1, "a missed day resets the cycle");

        var restored = ChallengeProgressVersion.FromToken(progress.ToToken());
        Expect(restored.LastDailyLoginDateUtc == progress.LastDailyLoginDateUtc && restored.DailyLoginStreak == 1, "EF token preserves login progress");

        Type controller = typeof(ChallengeProgressVersion).Assembly.GetType("Solace.ApiServer.Controllers.EarthApi.ChallengesController")!;
        MethodInfo addChallenges = controller.GetMethod("AddDailyLoginChallenges", BindingFlags.NonPublic | BindingFlags.Static)!;
        var challenges = new Dictionary<string, object>();
        addChallenges.Invoke(null, [challenges, "2026-08-10T00:00:00Z", new ChallengeProgressVersion(), Day(0)]);

        using var json = JsonDocument.Parse(JsonSerializer.Serialize(challenges, JsonOptions));
        JsonElement[] signIns = [.. json.RootElement.EnumerateObject().Select(item => item.Value).Where(item => item.GetProperty("duration").GetString() == "SignIn")];
        Expect(signIns.Length == 7, "response contains seven sign-in challenges");
        Expect(signIns.Select(item => item.GetProperty("order").GetInt32()).SequenceEqual(Enumerable.Range(0, 7)), "sign-in challenges are ordered 0 through 6");
        Expect(signIns.All(item => item.GetProperty("rarity").GetString() == "common"), "daily rarity is lowercase");
        Expect(signIns[6].GetProperty("clientProperties").GetProperty("isFinalReward").GetBoolean(), "day 7 is the final reward");

        var daySevenProgress = new ChallengeProgressVersion();
        for (int day = 0; day < 6; day++)
        {
            daySevenProgress.ClaimDailyLogin(Day(day));
        }

        var daySevenChallenges = new Dictionary<string, object>();
        addChallenges.Invoke(null, [daySevenChallenges, "2026-08-16T00:00:00.0000000Z", daySevenProgress, Day(6)]);
        using var daySevenJson = JsonDocument.Parse(JsonSerializer.Serialize(daySevenChallenges, JsonOptions));
        JsonElement[] daySevenSignIns = [.. daySevenJson.RootElement.EnumerateObject().Select(item => item.Value).Where(item => item.GetProperty("duration").GetString() == "SignIn")];
        Expect(daySevenSignIns.Count(item => item.GetProperty("state").GetString() == "Claimed") == 6, "days 1-6 are claimed on day 7");
        Expect(daySevenSignIns.Single(item => item.GetProperty("state").GetString() == "Active").GetProperty("order").GetInt32() == 6, "day 7 is active");

        var wrappedProgress = new ChallengeProgressVersion();
        for (int day = 0; day < 8; day++)
        {
            wrappedProgress.ClaimDailyLogin(Day(day));
        }

        Type goodiesController = typeof(ChallengeProgressVersion).Assembly.GetType("Solace.ApiServer.Controllers.EarthApi.DailyGoodiesController")!;
        MethodInfo buildGoodies = goodiesController.GetMethod("BuildDailyGoodiesResponse", BindingFlags.NonPublic | BindingFlags.Static)!;
        object wrappedGoodies = buildGoodies.Invoke(null, ["2026-08-16", Day(7), wrappedProgress, null])!;
        using var wrappedGoodiesJson = JsonDocument.Parse(JsonSerializer.Serialize(wrappedGoodies, JsonOptions));
        Expect(wrappedGoodiesJson.RootElement.GetProperty("streak").GetInt32() == 1, "day 8 reports cycle day 1");

        MethodInfo ensureDailyLoginToken = controller.GetMethod("EnsureDailyLoginToken", BindingFlags.NonPublic | BindingFlags.Static)!;
        MethodInfo dailyLoginRewards = controller.GetMethod("DailyLoginRewards", BindingFlags.NonPublic | BindingFlags.Static)!;
        var legacyRewards = (DBRewards)dailyLoginRewards.Invoke(null, null)!;
        var legacyTokens = new TokensEF();
        legacyTokens.AddToken("gap", new TokensEF.DailyLoginToken("2026-08-12", legacyRewards, true, Day(3)));
        legacyTokens.AddToken("day-1", new TokensEF.DailyLoginToken("2026-08-14", legacyRewards, true, Day(5)));
        legacyTokens.AddToken("day-2", new TokensEF.DailyLoginToken("2026-08-15", legacyRewards, true, Day(6)));
        var migratedProgress = new ChallengeProgressVersion();
        ensureDailyLoginToken.Invoke(null, [legacyTokens, migratedProgress, Day(7)]);
        Expect(migratedProgress.DailyLoginStreak == 2 && migratedProgress.GetDailyLoginDay(Day(7)) == 3, "legacy consecutive claims migrate to day 3");
        Expect(legacyTokens.GetTokens().Single().Token is TokensEF.DailyLoginToken { Date: "2026-08-16", Claimed: false }, "legacy tokens are replaced by today's token");

        using var db = EarthDbContext.CreateFromConnection("Data Source=:memory:");
        var trackedTokens = new TokensEF { Id = Guid.NewGuid() };
        trackedTokens.AddToken("progress", new TokensEF.ChallengeProgressToken
        {
            UpdatedAt = Day(7),
            DailyDateUtc = "2026-08-16"
        });
        db.Attach(trackedTokens);
        trackedTokens.AddToken("progress", new TokensEF.ChallengeProgressToken
        {
            UpdatedAt = Day(7),
            DailyDateUtc = "2026-08-16",
            LastDailyLoginDateUtc = "2026-08-16",
            DailyLoginStreak = 3
        });
        db.ChangeTracker.DetectChanges();
        Expect(db.Entry(trackedTokens).Property(tokens => tokens.Tokens).IsModified, "EF detects daily-login-only progress changes");

        Console.WriteLine("Daily login cycle and response checks passed.");
    }

    private static long Day(int offset)
        => FirstDay.AddDays(offset).ToUnixTimeMilliseconds();

    private static void Expect(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
