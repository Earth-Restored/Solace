using System.Collections.Immutable;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using Solace.Common;

namespace Solace.StaticData;

public sealed class PlayerLevels
{
    public readonly ImmutableArray<Level> Levels;

    internal PlayerLevels(string dir)
    {
        try
        {
            var levels = ImmutableArray.CreateBuilder<Level>();
            string file;
            for (int levelIndex = 2; File.Exists(file = Path.Combine(dir, $"{levelIndex}.json")); levelIndex++)
            {
                using (var stream = File.OpenRead(file))
                {
                    var level = JsonSerializer.Deserialize(stream, AppJsonContext.Default.Level);

                    Debug.Assert(level is not null);

                    levels.Add(level);
                }
            }

            Levels = levels.DrainToImmutable();

            for (int index = 1; index < Levels.Length; index++)
            {
                if (Levels[index].ExperienceRequired <= Levels[index - 1].ExperienceRequired)
                {
                    throw new StaticDataException($"Level {index + 2} has lower experience required than preceding level {index + 1}");
                }
            }
        }
        catch (StaticDataException)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw new StaticDataException(null, exception);
        }
    }

    public sealed record Level(
        int ExperienceRequired,
        int Rubies,
        LevelItem[] Items,
        string[] Buildplates
    );

    public sealed record LevelItem(
        Guid Id,
        int Count
    );
}
