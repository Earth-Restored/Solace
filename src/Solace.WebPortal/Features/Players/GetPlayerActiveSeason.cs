using Immediate.Apis.Shared;
using Immediate.Handlers.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Solace.Db.Earth;
using Solace.Db.Earth.Models.Challenges;
using Solace.Db.Earth.Models.Common;
using Solace.WebPortal.Common;
using Solace.WebPortal.Common.Features.Seasons;

namespace Solace.WebPortal.Features.Players;

[Handler]
[MapGet("{profileId:guid}/season")]
[MapGroup<PlayersGroup>]
[Authorize]
public static partial class GetPlayerActiveSeason
{
    public sealed record Query([property: FromRoute] Guid ProfileId);

    private static async ValueTask<Results<Ok<PlayerActiveSeasonDto>, NotFound, UnauthorizedHttpResult, ForbidHttpResult>> HandleAsync(
        Query query,
        EarthDbContext earthDb,
        IHttpContextAccessor httpContextAccessor,
        CancellationToken cancellationToken
    )
    {
        var httpUser = httpContextAccessor.HttpContext?.User;
        if (httpUser is null)
        {
            return TypedResults.Unauthorized();
        }

        var userId = httpUser.GetIdLong();

        var profile = await earthDb.Profiles
            .AsNoTracking()
            .Select(profile => new { profile.Id, profile.WebPortalAccountId, })
            .FirstOrDefaultAsync(profile => profile.Id == query.ProfileId, cancellationToken);

        if (profile is null)
        {
            return TypedResults.NotFound();
        }

        if (!httpUser.HasPermission(Permissions.ViewPlayers) && profile.WebPortalAccountId != userId)
        {
            return TypedResults.Forbid();
        }

        var season = await earthDb.Seasons
            .AsNoTracking()
            .Include(season => season.Challenges)
            .FirstOrDefaultAsync(season => season.IsActive, cancellationToken);

        if (season is null)
        {
            return TypedResults.Ok(new PlayerActiveSeasonDto(null, null));
        }

        var orderedChallenges = season.Challenges
            .OrderBy(c => c.Order)
            .ToList();

        var seasonKey = $"season_{season.SeasonNumber}";
        var eventNameKey = $"seasons.{seasonKey}.event_name";
        var eventDescKey = $"seasons.{seasonKey}.event_description";

        var translationKeys = new HashSet<string>(StringComparer.Ordinal)
        {
            eventNameKey,
            eventDescKey,
        };

        foreach (var challenge in orderedChallenges)
        {
            translationKeys.Add($"challenge_{challenge.Id}.desc.single");
            translationKeys.Add($"challenge_{challenge.Id}.desc.multiple");
        }

        var translations = await earthDb.ResourcePackTranslations
            .AsNoTracking()
            .Where(t => translationKeys.Contains(t.Key))
            .ToListAsync(cancellationToken);

        var translationsByKey = translations
            .GroupBy(translation => translation.Key, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.ToDictionary(translation => translation.Language, translation => translation.Value, StringComparer.Ordinal),
                StringComparer.Ordinal);

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

            translationsByKey.TryGetValue(singleKey, out var singleByLang);
            translationsByKey.TryGetValue(multiKey, out var multiByLang);

            var challengeLangs = new HashSet<string>(StringComparer.Ordinal);
            if (singleByLang is not null)
            {
                challengeLangs.UnionWith(singleByLang.Keys);
            }

            if (multiByLang is not null)
            {
                challengeLangs.UnionWith(multiByLang.Keys);
            }

            var challengeTranslations = new Dictionary<string, ChallengeTranslationDto>(StringComparer.Ordinal);
            foreach (var language in challengeLangs)
            {
                string? single = null;
                string? multi = null;
                singleByLang?.TryGetValue(language, out single);
                multiByLang?.TryGetValue(language, out multi);
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

        var seasonDto = new SeasonPreviewDto(
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

        var seasonProgress = await earthDb.PlayerSeasonProgresses
            .AsNoTracking()
            .FirstOrDefaultAsync(progress => progress.ProfileId == query.ProfileId && progress.SeasonId == season.Id, cancellationToken);

        var challengeIds = orderedChallenges.Select(challenge => challenge.Id).ToHashSet();
        var challengeProgressList = await earthDb.PlayerChallengeProgresses
            .AsNoTracking()
            .Where(progress => progress.ProfileId == query.ProfileId && challengeIds.Contains(progress.ChallengeId))
            .ToListAsync(cancellationToken);

        var challengeProgressMap = challengeProgressList.ToDictionary(
            progress => progress.ChallengeId,
            progress => new PlayerChallengeProgressDto(progress.CurrentCount, progress.IsComplete));

        var progressDto = new PlayerSeasonProgressDto(
            seasonProgress?.ActiveSeasonChallengeId,
            seasonProgress?.IsCompleted ?? false,
            seasonProgress?.StartedAt,
            seasonProgress?.CompletedAt,
            challengeProgressMap
        );

        return TypedResults.Ok(new PlayerActiveSeasonDto(seasonDto, progressDto));
    }

    private static HashSet<Guid> GetSparkleChallengeIds(List<ChallengeDefinitionEF> challenges)
    {
        var capstoneChallenge = challenges
            .LastOrDefault(c => c.IsFinalSeasonChallenge || c.ObjectiveType is ObjectiveTypeEF.SeasonCapstone)
            ?? challenges.LastOrDefault();

        var milestoneChallenge = challenges
            .FirstOrDefault(c => c != capstoneChallenge && c.Rewards.PersonaItems.Length > 0)
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
            [.. rewards.Items.Select(kvp => new SeasonRewardItemDto(kvp.Key, kvp.Value))],
            [.. rewards.Buildplates],
            [.. rewards.PersonaItems]
        );
}
