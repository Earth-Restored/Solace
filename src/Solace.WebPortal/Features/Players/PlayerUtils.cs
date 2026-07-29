using Solace.StaticData;

namespace Solace.WebPortal.Features.Players;

public static class PlayerUtils
{
    public static int? GetRequiredExperienceForNextLevel(int level, PlayerLevels levels)
    {
        // Levels starts at 2, player starts at 1
        var levelIndex = int.Max(level - 1, 0);
        return levelIndex < levels.Levels.Length ? levels.Levels[levelIndex].ExperienceRequired : null;
    }

    public static float? GetLevelProgressPercentage(int level, int experience, PlayerLevels levels)
    {
        var requiredExperienceNull = GetRequiredExperienceForNextLevel(level, levels);

        if (requiredExperienceNull is not { } requiredExperience)
        {
            return null;
        }
        else if (requiredExperience <= 0f)
        {
            return 1f;
        }

        return float.Clamp(experience / (float)requiredExperience, 0f, 1f);
    }
}