using Immediate.Apis.Shared;
using Immediate.Handlers.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Solace.Db.Earth;
using Solace.Db.Earth.Models.Challenges;
using Solace.EventBus.Client;
using Solace.WebPortal.Common;
using Solace.WebPortal.Common.Features.Seasons;

namespace Solace.WebPortal.Features.Seasons;

[Handler]
[MapPut("{seasonId:guid}")]
[MapGroup<SeasonsGroup>]
[Authorize(Policy = Permissions.EditSeasonsAndChallenges)]
public static partial class UpdateSeason
{
    public sealed record Command([property: FromRoute] Guid SeasonId, [property: FromBody] SaveSeasonCommand Body);

    private static async ValueTask<Results<Ok<SeasonPreviewDto>, BadRequest<string>, NotFound>> HandleAsync(
        Command command,
        EarthDbContext earthDb,
        EventBusClient eventBus,
        CancellationToken cancellationToken
    )
    {
        var body = command.Body;
        if (SeasonUtils.ValidateSaveCommand(body) is { } validationError)
        {
            return TypedResults.BadRequest(validationError);
        }

        await using var transaction = await earthDb.Database.BeginTransactionAsync(cancellationToken);

        var season = await earthDb.Seasons
            .AsTracking()
            .Include(s => s.Challenges)
            .FirstOrDefaultAsync(s => s.Id == command.SeasonId, cancellationToken);

        if (season is null)
        {
            return TypedResults.NotFound();
        }

        var oldSeasonNumber = season.SeasonNumber;
        if (oldSeasonNumber != body.SeasonNumber)
        {
            var numberConflict = await earthDb.Seasons
                .AsNoTracking()
                .AnyAsync(s => s.Id != season.Id && s.SeasonNumber == body.SeasonNumber, cancellationToken);

            if (numberConflict)
            {
                return TypedResults.BadRequest($"Season #{body.SeasonNumber} already exists.");
            }
        }

        var oldChallengeIds = season.Challenges.Select(c => c.Id).ToList();
        var incomingIds = body.Challenges.Select(c => c.Id).ToHashSet();

        // Remove challenges that were deleted
        var toRemove = season.Challenges.Where(c => !incomingIds.Contains(c.Id)).ToList();
        if (toRemove.Count > 0)
        {
            earthDb.Challenges.RemoveRange(toRemove);
            foreach (var removed in toRemove)
            {
                season.Challenges.Remove(removed);
            }
        }

        var existingById = season.Challenges.ToDictionary(c => c.Id);

        foreach (var challengeDto in body.Challenges)
        {
            var objectiveType = SeasonUtils.MapObjectiveTypeToDb(challengeDto.ObjectiveType);
            var isFinal = challengeDto.IsFinalSeasonChallenge || objectiveType is ObjectiveTypeEF.SeasonCapstone;
            var rewards = SeasonUtils.MapRewardsToDb(challengeDto.Rewards);
            var prereqs = challengeDto.PrerequisiteIds.Distinct().ToList();
            var logicalCondition = challengeDto.PrerequisiteIsOr ? LogicalConditionEF.Or : LogicalConditionEF.And;

            if (existingById.TryGetValue(challengeDto.Id, out var existingChallenge))
            {
                existingChallenge.Order = challengeDto.Order;
                existingChallenge.TotalThreshold = challengeDto.TotalThreshold;
                existingChallenge.PrerequisiteIds = prereqs;
                existingChallenge.PrerequisiteLogicalCondition = logicalCondition;
                existingChallenge.ObjectiveType = objectiveType;
                existingChallenge.ObjectiveTarget = challengeDto.ObjectiveTarget;
                existingChallenge.IsFinalSeasonChallenge = isFinal;
                existingChallenge.Rewards = rewards;
            }
            else
            {
                var newChallenge = new ChallengeDefinitionEF
                {
                    Id = challengeDto.Id,
                    SeasonId = season.Id,
                    Order = challengeDto.Order,
                    TotalThreshold = challengeDto.TotalThreshold,
                    PrerequisiteIds = prereqs,
                    PrerequisiteLogicalCondition = logicalCondition,
                    ObjectiveType = objectiveType,
                    ObjectiveTarget = challengeDto.ObjectiveTarget,
                    IsFinalSeasonChallenge = isFinal,
                    Rewards = rewards,
                };

                earthDb.Challenges.Add(newChallenge);
            }
        }

        season.SeasonNumber = body.SeasonNumber;
        season.Version++;
        season.ExclusiveRewards = body.ExclusiveRewards is not null ? SeasonUtils.MapRewardsToDb(body.ExclusiveRewards) : null;

        await SeasonUtils.ReplaceSeasonTranslationsAsync(earthDb, oldSeasonNumber, season.SeasonNumber, oldChallengeIds, body, cancellationToken);
        await SeasonUtils.UpsertSeasonTexturesAsync(earthDb, oldSeasonNumber, season.SeasonNumber, body.TextureOverrides, cancellationToken);

        await earthDb.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        await eventBus.PublishAsync("seasons", "seasons_data_updated", string.Empty, cancellationToken);
        await eventBus.PublishAsync("resourcepack", "synthesize", string.Empty, cancellationToken);

        var dto = await SeasonUtils.ToPreviewDtoAsync(earthDb, season, cancellationToken);
        return TypedResults.Ok(dto);
    }
}
