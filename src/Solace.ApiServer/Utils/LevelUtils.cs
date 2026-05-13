using Microsoft.EntityFrameworkCore;
using Solace.DB;
using Solace.DB.Models.Player;
using Solace.DB.Utils;
using Solace.StaticData;
using static Solace.DB.Models.Player.TokensEF;

namespace Solace.ApiServer.Utils;

public sealed class LevelUtils
{
#pragma warning disable IDE0060 // Remove unused parameter
    public static async Task CheckAndHandlePlayerLevelUpAsync(EarthDbContext.Results results, Guid accountId, long currentTime, StaticData.StaticData staticData)
#pragma warning restore IDE0060 // Remove unused parameter
    {
        var profile = await results.EarthDb.Profiles
            .AsTracking()
            .FirstOrNewAsync(profile => profile.Id == accountId);

        bool changed = false;
        while (profile.Level - 1 < staticData.Levels.Levels.Length && profile.Experience >= staticData.Levels.Levels[profile.Level - 1].ExperienceRequired)
        {
            changed = true;
            profile.Level++;
            Rewards rewards = MakeLevelRewards(staticData.Levels.Levels[profile.Level - 2]);
            await TokenUtils.AddTokenAsync(results, accountId, new LevelUpToken(profile.Level, rewards.ToDBRewardsModel()));
        }

        if (changed)
        {
            await results.EarthDb.SaveChangesAsync();

            results.Profile = profile.Version;
        }
    }

    public static Rewards MakeLevelRewards(PlayerLevels.Level level)
    {
        var rewards = new Rewards();
        if (level.Rubies > 0)
        {
            rewards.AddRubies(level.Rubies);
        }

        foreach (var item in level.Items)
        {
            rewards.AddItem(item.Id, item.Count);
        }

        foreach (string buildplate in level.Buildplates)
        {
            rewards.AddBuildplate(buildplate);
        }

        return rewards;
    }
}
