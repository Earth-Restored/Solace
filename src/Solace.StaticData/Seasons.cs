#pragma warning disable MA0048 // File name must match type name
using System.Collections.Frozen;
using System.Text.Json;

namespace Solace.StaticData;

public sealed class SeasonsCatalog
{
    public readonly Version Version;
    public readonly FrozenDictionary<Guid, SeasonDefinition> Seasons;
    public readonly FrozenDictionary<Guid, SeasonChallengeDefinition> Challenges;

    internal SeasonsCatalog(string dir)
    {
        Version = Version.Parse(File.ReadAllText(Path.Combine(dir, "version.txt")).Trim());
        var seasonsList = new List<SeasonDefinition>();
        var challengesList = new List<SeasonChallengeDefinition>();

        foreach (var subDir in Directory.EnumerateDirectories(dir))
        {
            var infoFile = Path.Combine(subDir, "info.json");
            if (!File.Exists(infoFile))
            {
                continue;
            }

            using var stream = File.OpenRead(infoFile);
            var season = JsonSerializer.Deserialize(stream, AppJsonContext.Default.SeasonDefinition);
            if (season is not null)
            {
                var texturesDir = Path.Combine(subDir, "textures");
                var textureFiles = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                if (Directory.Exists(texturesDir))
                {
                    foreach (var filePath in Directory.EnumerateFiles(texturesDir, "*.*", SearchOption.AllDirectories))
                    {
                        var relPath = Path.GetRelativePath(texturesDir, filePath).Replace('\\', '/');
                        var rpKey = $"textures/ui/seasons/{season.Key}/{relPath}";
                        textureFiles[rpKey] = filePath;
                    }
                }

                var challenges = new List<SeasonChallengeDefinition>(season.Challenges.Count);
                for (var i = 0; i < season.Challenges.Count; i++)
                {
                    var challenge = season.Challenges[i];
                    var isFinal = challenge.IsFinalSeasonChallenge || i == season.Challenges.Count - 1 || challenge.ObjectiveType == ObjectiveType.SeasonCapstone;
                    challenge = challenge with
                    {
                        IsFinalSeasonChallenge = isFinal
                    };

                    challenges.Add(challenge);
                }

                var updatedSeason = season with
                {
                    Challenges = challenges,
                    TextureFiles = textureFiles.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase)
                };
                seasonsList.Add(updatedSeason);
                challengesList.AddRange(challenges);
            }
        }

        Seasons = seasonsList.ToFrozenDictionary(season => season.Id);
        Challenges = challengesList.ToFrozenDictionary(challenge => challenge.Id);
    }

    public sealed record SeasonDefinition(
        Guid Id,
        int SeasonNumber,
        IReadOnlyList<SeasonChallengeDefinition> Challenges,
        IReadOnlyDictionary<string, SeasonTranslation> Translations,
        Rewards? ExclusiveRewards = null
    )
    {
        public string Key => field ??= $"season_{SeasonNumber}";

        public IReadOnlyDictionary<string, string> TextureFiles { get; init; } = FrozenDictionary<string, string>.Empty;

        public IEnumerable<(string Language, string Key, string Value)> GetAllTranslations()
        {
            foreach (var (lang, translation) in Translations)
            {
                if (translation.Name is not null)
                {
                    yield return (lang, $"seasons.{Key}.event_name", translation.Name);
                }

                if (translation.Description is not null)
                {
                    yield return (lang, $"seasons.{Key}.event_description", translation.Description);
                }

                if (translation.IntroBody is not null)
                {
                    yield return (lang, $"seasons.{Key}.intro.body", translation.IntroBody);
                }

                if (translation.EventProgress is not null)
                {
                    yield return (lang, $"seasons.{Key}.progress", translation.EventProgress);
                }

                if (translation.EventComplete is not null)
                {
                    yield return (lang, $"seasons.{Key}.complete", translation.EventComplete);
                }
            }

            foreach (var challenge in Challenges)
            {
                foreach (var (lang, translation) in challenge.Translations)
                {
                    if (translation.Description is not null)
                    {
                        yield return (lang, $"challenge_{challenge.Id}.desc.single", translation.Description);
                    }

                    if (translation.DescriptionMultiple is not null)
                    {
                        yield return (lang, $"challenge_{challenge.Id}.desc.multiple", translation.DescriptionMultiple);
                    }
                }
            }
        }
    }

    public sealed record SeasonChallengeDefinition(
        Guid Id,
        int Order,
        int TotalThreshold,
        ObjectiveType ObjectiveType,
        IReadOnlyDictionary<string, ChallengeTranslation> Translations,
        Guid? ParentId = null,
        IReadOnlyList<Guid>? PrerequisiteIds = null,
        LogicalCondition PrerequisiteLogicalCondition = LogicalCondition.And,
        Guid? ObjectiveTarget = null,
        bool IsFinalSeasonChallenge = false,
        Rewards? Rewards = null
    )
    {
        public IReadOnlyList<Guid> PrerequisiteIds { get; init; } = PrerequisiteIds ?? [];
        public Rewards Rewards { get; init; } = Rewards ?? new();
    }

    public sealed record SeasonTranslation(
        string? Name = null,
        string? Description = null,
        string? IntroBody = null,
        string? EventProgress = null,
        string? EventComplete = null
    );

    public sealed record ChallengeTranslation(
        string? Description = null,
        string? DescriptionMultiple = null
    );

    public sealed record Rewards(
        int? Rubies = null,
        int? ExperiencePoints = null,
        int? Level = null,
        IReadOnlyList<Rewards.RewardItem>? Inventory = null,
        IReadOnlyList<Guid>? Buildplates = null,
        IReadOnlyList<Rewards.RewardChallenge>? Challenges = null,
        IReadOnlyList<Guid>? PersonaItems = null,
        IReadOnlyList<Rewards.RewardUtilityBlock>? UtilityBlocks = null
    )
    {
        public IReadOnlyList<RewardItem> Inventory { get; init; } = Inventory ?? [];
        public IReadOnlyList<Guid> Buildplates { get; init; } = Buildplates ?? [];
        public IReadOnlyList<RewardChallenge> Challenges { get; init; } = Challenges ?? [];
        public IReadOnlyList<Guid> PersonaItems { get; init; } = PersonaItems ?? [];
        public IReadOnlyList<RewardUtilityBlock> UtilityBlocks { get; init; } = UtilityBlocks ?? [];

        public sealed record RewardItem(
            Guid Id,
            int Amount
        );

        public sealed record RewardChallenge(
            Guid Id
        );

        public sealed record RewardUtilityBlock();
    }
}
