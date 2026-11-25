using Microsoft.EntityFrameworkCore;
using Solace.Db.Earth;
using Solace.Db.Earth.Models.Challenges;
using Solace.Db.Earth.Models.Common;
using Solace.WebPortal.Common.Features.Seasons;

namespace Solace.WebPortal.Features.Seasons;

internal static class SeasonUtils
{
    public static Task DeactivateExpiredSeasonsAsync(EarthDbContext earthDb, CancellationToken cancellationToken)
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

    public static async Task<List<SeasonPreviewDto>> ToPreviewDtosAsync(
        EarthDbContext earthDb,
        IReadOnlyList<SeasonEF> seasons,
        CancellationToken cancellationToken)
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

    public static async Task<SeasonPreviewDto> ToPreviewDtoAsync(
        EarthDbContext earthDb,
        SeasonEF season,
        CancellationToken cancellationToken)
    {
        var list = await ToPreviewDtosAsync(earthDb, [season], cancellationToken);
        return list[0];
    }

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

        var seasonLanguages = new HashSet<string>(StringComparer.Ordinal);
        if (translationsByKey.TryGetValue(eventNameKey, out var nameByLang))
        {
            seasonLanguages.UnionWith(nameByLang.Keys);
        }

        if (translationsByKey.TryGetValue(eventDescKey, out var descByLang))
        {
            seasonLanguages.UnionWith(descByLang.Keys);
        }

        var seasonTranslations = new Dictionary<string, SeasonTranslationDto>(StringComparer.Ordinal);
        foreach (var language in seasonLanguages)
        {
            string? name = null;
            string? description = null;
            nameByLang?.TryGetValue(language, out name);
            descByLang?.TryGetValue(language, out description);
            seasonTranslations[language] = new SeasonTranslationDto(name, description);
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
                RewardSparkles: sparkleChallengeIds.Contains(challenge.Id)
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
}
