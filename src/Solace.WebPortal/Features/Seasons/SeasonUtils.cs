using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Solace.Db.Earth;
using Solace.Db.Earth.Models.Challenges;
using Solace.Db.Earth.Models.Common;
using Solace.Db.Earth.Models.ResourcePacks;
using Solace.WebPortal.Common.Features.Seasons;

namespace Solace.WebPortal.Features.Seasons;

internal static partial class SeasonUtils
{
    public const int MaxTextureFileSizeBytes = 2 * 1024 * 1024; // 2 MB

    private static ReadOnlySpan<byte> PngSignature => [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    public static Task<int> DeactivateExpiredSeasonsAsync(EarthDbContext earthDb, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        return earthDb.Seasons
            .Where(season => season.IsActive && season.EndDate != null && season.EndDate <= now)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(season => season.IsActive, false)
                    .SetProperty(season => season.StartDate, (DateTimeOffset?)null)
                    .SetProperty(season => season.EndDate, (DateTimeOffset?)null),
                cancellationToken);
    }

    public static string? ValidateSaveCommand(SaveSeasonCommand command)
    {
        if (command.SeasonNumber < 1)
        {
            return "Season number must be at least 1.";
        }

        var challengeIds = new HashSet<Guid>();
        var usedOrders = new HashSet<int>();

        foreach (var challenge in command.Challenges)
        {
            if (challenge.Id == Guid.Empty)
            {
                return "Every challenge must have a valid ID.";
            }

            if (!challengeIds.Add(challenge.Id))
            {
                return $"Duplicate challenge ID detected: {challenge.Id}.";
            }

            if (challenge.Order < 1)
            {
                return "Challenge position (order) must be at least 1 (slot 0 is reserved).";
            }

            if (!usedOrders.Add(challenge.Order))
            {
                return $"Multiple challenges cannot occupy the same grid slot (#{challenge.Order}).";
            }

            if (challenge.TotalThreshold < 1)
            {
                return $"Challenge #{challenge.Order} threshold must be at least 1.";
            }

            if (!Enum.IsDefined(challenge.ObjectiveType))
            {
                return $"Challenge #{challenge.Order} has an invalid objective type.";
            }
        }

        var prereqsById = new Dictionary<Guid, IReadOnlyList<Guid>>(command.Challenges.Count);
        foreach (var challenge in command.Challenges)
        {
            foreach (var prereqId in challenge.PrerequisiteIds)
            {
                if (prereqId == challenge.Id)
                {
                    return $"Challenge #{challenge.Order} cannot depend on itself.";
                }

                if (!challengeIds.Contains(prereqId))
                {
                    return $"Challenge #{challenge.Order} references a prerequisite that does not exist in this season.";
                }
            }

            prereqsById[challenge.Id] = challenge.PrerequisiteIds;
        }

        if (HasCycle(prereqsById))
        {
            return "Challenge prerequisites contain a circular dependency.";
        }

        if (command.TextureOverrides is not null)
        {
            foreach (var (fileName, data) in command.TextureOverrides)
            {
                if (!TextureFileNameRegex().IsMatch(fileName))
                {
                    return $"Invalid texture filename '{fileName}'. Only .png filenames with letters, digits, underscores, and hyphens are allowed.";
                }

                if (data is null or [])
                {
                    return $"Texture '{fileName}' is empty.";
                }

                if (data.Length > MaxTextureFileSizeBytes)
                {
                    return $"Texture '{fileName}' exceeds the maximum allowed size of 2 MB.";
                }

                if (data.Length < PngSignature.Length || !data.AsSpan(0, PngSignature.Length).SequenceEqual(PngSignature))
                {
                    return $"Texture '{fileName}' is not a valid PNG image.";
                }
            }
        }

        return null;
    }

    private static bool HasCycle(Dictionary<Guid, IReadOnlyList<Guid>> prereqsById)
    {
        // 0 = unvisited, 1 = visiting, 2 = visited
        var state = new Dictionary<Guid, int>(prereqsById.Count);

        bool Dfs(Guid id)
        {
            if (state.TryGetValue(id, out var s))
            {
                return s == 1;
            }

            state[id] = 1;

            if (prereqsById.TryGetValue(id, out var prereqs))
            {
                foreach (var prereq in prereqs)
                {
                    if (Dfs(prereq))
                    {
                        return true;
                    }
                }
            }

            state[id] = 2;

            return false;
        }

        foreach (var id in prereqsById.Keys)
        {
            if (Dfs(id))
            {
                return true;
            }
        }

        return false;
    }

    public static async Task<List<SeasonPreviewDto>> ToPreviewDtosAsync(EarthDbContext earthDb, IReadOnlyList<SeasonEF> seasons, CancellationToken cancellationToken)
    {
        if (seasons.Count is 0)
        {
            return [];
        }

        var translationKeys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var season in seasons)
        {
            var seasonKey = $"season_{season.SeasonNumber}";
            translationKeys.Add($"seasons.{seasonKey}.event_name");
            translationKeys.Add($"seasons.{seasonKey}.event_description");
            translationKeys.Add($"seasons.{seasonKey}.intro.body");
            translationKeys.Add($"seasons.{seasonKey}.progress");
            translationKeys.Add($"seasons.{seasonKey}.complete");

            foreach (var challenge in season.Challenges)
            {
                translationKeys.Add($"challenge_{challenge.Id}.desc.single");
                translationKeys.Add($"challenge_{challenge.Id}.desc.multiple");
            }
        }

        var translations = await earthDb.ResourcePackTranslations
            .AsNoTracking()
            .Where(translation => translationKeys.Contains(translation.Key))
            .ToListAsync(cancellationToken);

        var translationsByKey = translations
            .GroupBy(translation => translation.Key, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.ToDictionary(translation => translation.Language, translation => translation.Value, StringComparer.Ordinal),
                StringComparer.Ordinal);

        var result = new List<SeasonPreviewDto>(seasons.Count);
        foreach (var season in seasons)
        {
            result.Add(ToPreviewDto(season, translationsByKey));
        }

        return result;
    }

    public static async Task<SeasonPreviewDto> ToPreviewDtoAsync(EarthDbContext earthDb, SeasonEF season, CancellationToken cancellationToken)
    {
        var list = await ToPreviewDtosAsync(earthDb, [season], cancellationToken);
        return list[0];
    }

    public static async Task ReplaceSeasonTranslationsAsync(EarthDbContext earthDb, int oldSeasonNumber, int newSeasonNumber, IReadOnlyCollection<Guid> oldChallengeIds, SaveSeasonCommand command, CancellationToken cancellationToken)
    {
        var keysToRemove = new HashSet<string>(StringComparer.Ordinal);
        AddSeasonTranslationKeys(keysToRemove, oldSeasonNumber);
        AddSeasonTranslationKeys(keysToRemove, newSeasonNumber);

        foreach (var id in oldChallengeIds)
        {
            keysToRemove.Add($"challenge_{id}.desc.single");
            keysToRemove.Add($"challenge_{id}.desc.multiple");
        }

        foreach (var challenge in command.Challenges)
        {
            keysToRemove.Add($"challenge_{challenge.Id}.desc.single");
            keysToRemove.Add($"challenge_{challenge.Id}.desc.multiple");
        }

        var existing = await earthDb.ResourcePackTranslations
            .AsTracking()
            .Where(translation => keysToRemove.Contains(translation.Key))
            .ToListAsync(cancellationToken);

        if (existing.Count > 0)
        {
            earthDb.ResourcePackTranslations.RemoveRange(existing);
        }

        var seasonKey = $"season_{newSeasonNumber}";
        var newEntries = new Dictionary<(string Language, string Key), string>();

        void SetEntry(string language, string key, string? value)
        {
            var trimmedLang = language.Trim();
            if (string.IsNullOrWhiteSpace(trimmedLang) || string.IsNullOrWhiteSpace(value))
            {
                return;
            }

            newEntries[(trimmedLang, key)] = value.Trim();
        }

        foreach (var (language, translation) in command.Translations)
        {
            SetEntry(language, $"seasons.{seasonKey}.event_name", translation.Name);
            SetEntry(language, $"seasons.{seasonKey}.event_description", translation.Description);
            SetEntry(language, $"seasons.{seasonKey}.intro.body", translation.IntroBody);
            SetEntry(language, $"seasons.{seasonKey}.progress", translation.EventProgress);
            SetEntry(language, $"seasons.{seasonKey}.complete", translation.EventComplete);
        }

        foreach (var challenge in command.Challenges)
        {
            foreach (var (language, translation) in challenge.Translations)
            {
                SetEntry(language, $"challenge_{challenge.Id}.desc.single", translation.Description);
                SetEntry(language, $"challenge_{challenge.Id}.desc.multiple", translation.DescriptionMultiple);
            }
        }

        foreach (var ((language, key), value) in newEntries)
        {
            earthDb.ResourcePackTranslations.Add(new ResourcePackTranslationEF
            {
                Language = language,
                Key = key,
                Value = value,
            });
        }
    }

    public static async Task UpsertSeasonTexturesAsync(EarthDbContext earthDb, int oldSeasonNumber, int newSeasonNumber, IReadOnlyDictionary<string, byte[]>? textureOverrides, CancellationToken cancellationToken)
    {
        var oldPrefix = $"textures/ui/seasons/season_{oldSeasonNumber}/";
        var newPrefix = $"textures/ui/seasons/season_{newSeasonNumber}/";

        if (oldSeasonNumber != newSeasonNumber)
        {
            var oldAssets = await earthDb.ResourcePackAssets
                .AsTracking()
                .Where(asset => EF.Functions.Like(asset.Path, $"{oldPrefix}%"))
                .ToListAsync(cancellationToken);

            foreach (var oldAsset in oldAssets)
            {
                var relName = oldAsset.Path[oldPrefix.Length..];
                var targetPath = newPrefix + relName;

                earthDb.ResourcePackAssets.Remove(oldAsset);

                if (textureOverrides is null || !textureOverrides.ContainsKey(relName))
                {
                    var existingTarget = await earthDb.ResourcePackAssets
                        .AsTracking()
                        .FirstOrDefaultAsync(asset => asset.Path == targetPath, cancellationToken);

                    if (existingTarget is null)
                    {
                        earthDb.ResourcePackAssets.Add(new ResourcePackAssetEF
                        {
                            Path = targetPath,
                            Data = oldAsset.Data,
                        });
                    }
                    else
                    {
                        existingTarget.Data = oldAsset.Data;
                    }
                }
            }
        }

        if (textureOverrides is null or { Count: 0, })
        {
            return;
        }

        foreach (var (fileName, bytes) in textureOverrides)
        {
            var assetPath = newPrefix + fileName;
            var existingAsset = await earthDb.ResourcePackAssets
                .AsTracking()
                .FirstOrDefaultAsync(asset => asset.Path == assetPath, cancellationToken);

            if (existingAsset is null)
            {
                earthDb.ResourcePackAssets.Add(new ResourcePackAssetEF
                {
                    Path = assetPath,
                    Data = bytes,
                });
            }
            else
            {
                existingAsset.Data = bytes;
            }
        }
    }

    public static void AddSeasonTranslationKeys(HashSet<string> keys, int seasonNumber)
    {
        var seasonKey = $"season_{seasonNumber}";
        keys.Add($"seasons.{seasonKey}.event_name");
        keys.Add($"seasons.{seasonKey}.event_description");
        keys.Add($"seasons.{seasonKey}.intro.body");
        keys.Add($"seasons.{seasonKey}.progress");
        keys.Add($"seasons.{seasonKey}.complete");
    }

    public static Rewards MapRewardsToDb(SeasonRewardsDto? rewards)
    {
        if (rewards is null)
        {
            return new Rewards();
        }

        var items = new Dictionary<Guid, int>();
        foreach (var item in rewards.Items)
        {
            if (item.Id != Guid.Empty && item.Amount > 0)
            {
                items[item.Id] = items.GetValueOrDefault(item.Id, 0) + item.Amount;
            }
        }

        return new Rewards(
            int.Max(0, rewards.Rubies),
            int.Max(0, rewards.ExperiencePoints),
            rewards.Level is > 0 ? rewards.Level : null,
            items,
            [.. rewards.Buildplates.Where(id => id != Guid.Empty).Distinct()],
            [],
            [.. rewards.PersonaItems.Where(id => id != Guid.Empty).Distinct()]
        );
    }

    public static ObjectiveTypeEF MapObjectiveTypeToDb(ChallengeObjectiveTypeDto type)
        => type switch
        {
            ChallengeObjectiveTypeDto.TappableCollected => ObjectiveTypeEF.TappableCollected,
            ChallengeObjectiveTypeDto.CraftItem => ObjectiveTypeEF.CraftItem,
            ChallengeObjectiveTypeDto.SmeltItem => ObjectiveTypeEF.SmeltItem,
            ChallengeObjectiveTypeDto.AdventurePlayed => ObjectiveTypeEF.AdventurePlayed,
            ChallengeObjectiveTypeDto.DefeatMob => ObjectiveTypeEF.DefeatMob,
            ChallengeObjectiveTypeDto.SeasonCapstone => ObjectiveTypeEF.SeasonCapstone,
            ChallengeObjectiveTypeDto.PlaceBlock => ObjectiveTypeEF.PlaceBlock,
            _ => ObjectiveTypeEF.TappableCollected,
        };

    public static ChallengeObjectiveTypeDto MapObjectiveTypeToDto(ObjectiveTypeEF type)
        => type switch
        {
            ObjectiveTypeEF.TappableCollected => ChallengeObjectiveTypeDto.TappableCollected,
            ObjectiveTypeEF.CraftItem => ChallengeObjectiveTypeDto.CraftItem,
            ObjectiveTypeEF.SmeltItem => ChallengeObjectiveTypeDto.SmeltItem,
            ObjectiveTypeEF.AdventurePlayed => ChallengeObjectiveTypeDto.AdventurePlayed,
            ObjectiveTypeEF.DefeatMob => ChallengeObjectiveTypeDto.DefeatMob,
            ObjectiveTypeEF.SeasonCapstone => ChallengeObjectiveTypeDto.SeasonCapstone,
            ObjectiveTypeEF.PlaceBlock => ChallengeObjectiveTypeDto.PlaceBlock,
            _ => ChallengeObjectiveTypeDto.TappableCollected,
        };

    private static SeasonPreviewDto ToPreviewDto(
        SeasonEF season,
        Dictionary<string, Dictionary<string, string>> translationsByKey)
    {
        var orderedChallenges = season.Challenges
            .OrderBy(challenge => challenge.Order)
            .ToList();

        var seasonKey = $"season_{season.SeasonNumber}";
        var eventNameKey = $"seasons.{seasonKey}.event_name";
        var eventDescKey = $"seasons.{seasonKey}.event_description";
        var introBodyKey = $"seasons.{seasonKey}.intro.body";
        var progressKey = $"seasons.{seasonKey}.progress";
        var completeKey = $"seasons.{seasonKey}.complete";

        translationsByKey.TryGetValue(eventNameKey, out var nameByLang);
        translationsByKey.TryGetValue(eventDescKey, out var desciptionByLang);
        translationsByKey.TryGetValue(introBodyKey, out var introByLang);
        translationsByKey.TryGetValue(progressKey, out var progressByLang);
        translationsByKey.TryGetValue(completeKey, out var completeByLang);

        var seasonLanguages = new HashSet<string>(StringComparer.Ordinal);
        if (nameByLang is not null)
        {
            seasonLanguages.UnionWith(nameByLang.Keys);
        }

        if (desciptionByLang is not null)
        {
            seasonLanguages.UnionWith(desciptionByLang.Keys);
        }

        if (introByLang is not null)
        {
            seasonLanguages.UnionWith(introByLang.Keys);
        }

        if (progressByLang is not null)
        {
            seasonLanguages.UnionWith(progressByLang.Keys);
        }

        if (completeByLang is not null)
        {
            seasonLanguages.UnionWith(completeByLang.Keys);
        }

        var seasonTranslations = new Dictionary<string, SeasonTranslationDto>(StringComparer.Ordinal);
        foreach (var language in seasonLanguages)
        {
            string? name = null;
            string? desc = null;
            string? intro = null;
            string? progress = null;
            string? complete = null;
            nameByLang?.TryGetValue(language, out name);
            desciptionByLang?.TryGetValue(language, out desc);
            introByLang?.TryGetValue(language, out intro);
            progressByLang?.TryGetValue(language, out progress);
            completeByLang?.TryGetValue(language, out complete);
            seasonTranslations[language] = new SeasonTranslationDto(name, desc, intro, progress, complete);
        }

        var sparkleChallengeIds = GetSparkleChallengeIds(orderedChallenges);

        var challengeDtos = new List<SeasonChallengeDto>(orderedChallenges.Count);
        foreach (var challenge in orderedChallenges)
        {
            var singleKey = $"challenge_{challenge.Id}.desc.single";
            var multiKey = $"challenge_{challenge.Id}.desc.multiple";

            translationsByKey.TryGetValue(singleKey, out var singleByLanguage);
            translationsByKey.TryGetValue(multiKey, out var multiByLanguage);

            var challengeLangs = new HashSet<string>(StringComparer.Ordinal);
            if (singleByLanguage is not null)
            {
                challengeLangs.UnionWith(singleByLanguage.Keys);
            }

            if (multiByLanguage is not null)
            {
                challengeLangs.UnionWith(multiByLanguage.Keys);
            }

            var challengeTranslations = new Dictionary<string, ChallengeTranslationDto>(StringComparer.Ordinal);
            foreach (var language in challengeLangs)
            {
                string? single = null;
                string? multi = null;
                singleByLanguage?.TryGetValue(language, out single);
                multiByLanguage?.TryGetValue(language, out multi);
                challengeTranslations[language] = new ChallengeTranslationDto(single, multi);
            }

            challengeDtos.Add(new SeasonChallengeDto(
                challenge.Id,
                challenge.Order,
                challenge.TotalThreshold,
                [.. challenge.PrerequisiteIds],
                challenge.PrerequisiteLogicalCondition is LogicalConditionEF.Or,
                challenge.IsFinalSeasonChallenge,
                challenge.ObjectiveType is ObjectiveTypeEF.SeasonCapstone,
                challengeTranslations,
                MapRewards(challenge.Rewards),
                RewardSparkles: sparkleChallengeIds.Contains(challenge.Id),
                ObjectiveType: MapObjectiveTypeToDto(challenge.ObjectiveType),
                ObjectiveTarget: challenge.ObjectiveTarget
            ));
        }

        return new SeasonPreviewDto(
            season.Id,
            season.SeasonNumber,
            season.StartDate,
            season.EndDate,
            season.IsActive,
            LockedPathColor: null,
            UnlockedPathColor: null,
            seasonTranslations,
            season.ExclusiveRewards is not null ? MapRewards(season.ExclusiveRewards) : null,
            challengeDtos
        );
    }

    private static HashSet<Guid> GetSparkleChallengeIds(List<ChallengeDefinitionEF> challenges)
    {
        var capstoneChallenge = challenges
            .LastOrDefault(challenge => challenge.IsFinalSeasonChallenge || challenge.ObjectiveType is ObjectiveTypeEF.SeasonCapstone)
            ?? challenges.LastOrDefault();

        var milestoneChallenge = challenges
            .FirstOrDefault(challenge => challenge != capstoneChallenge && challenge.Rewards.PersonaItems.Length > 0)
            ?? (challenges.Count >= 2 ? challenges[(challenges.Count - 2) / 2] : null);

        var ids = new HashSet<Guid>();
        if (milestoneChallenge is not null)
        {
            ids.Add(milestoneChallenge.Id);
        }

        if (capstoneChallenge is not null)
        {
            ids.Add(capstoneChallenge.Id);
        }

        return ids;
    }

    private static SeasonRewardsDto MapRewards(Rewards rewards)
        => new(
            rewards.Rubies,
            rewards.ExperiencePoints,
            rewards.Level,
            [.. rewards.Items.Select(item => new SeasonRewardItemDto(item.Key, item.Value))],
            [.. rewards.Buildplates],
            [.. rewards.PersonaItems]
        );

    [GeneratedRegex(@"^[a-zA-Z0-9_\-]+\.png$", RegexOptions.None, matchTimeoutMilliseconds: 200)]
    private static partial Regex TextureFileNameRegex();
}
