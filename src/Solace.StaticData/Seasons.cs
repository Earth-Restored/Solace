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

        var seasonsDir = Path.Combine(dir, "seasons");
        if (Directory.Exists(seasonsDir))
        {
            foreach (var file in Directory.EnumerateFiles(seasonsDir, "*.json"))
            {
                using var stream = File.OpenRead(file);
                var season = JsonSerializer.Deserialize(stream, AppJsonContext.Default.SeasonDefinition);
                if (season is not null)
                {
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

                    var updatedSeason = season with { Challenges = challenges };
                    seasonsList.Add(updatedSeason);
                    challengesList.AddRange(challenges);
                }
            }
        }

        Seasons = seasonsList.ToFrozenDictionary(s => s.Id);
        Challenges = challengesList.ToFrozenDictionary(c => c.Id);
    }

    public sealed record SeasonDefinition(
        Guid Id,
        int SeasonNumber,
        string Key, // "season_1", "season_2", etc.
        DateTimeOffset StartDate,
        DateTimeOffset EndDate,
        bool IsActive,
        IReadOnlyList<SeasonChallengeDefinition> Challenges,
        Rewards? ExclusiveRewards = null
    );

    public sealed record SeasonChallengeDefinition(
        Guid Id,
        int Order,
        int TotalThreshold,
        ObjectiveType ObjectiveType,
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
