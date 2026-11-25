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
[MapPost("")]
[MapGroup<SeasonsGroup>]
[Authorize(Policy = Permissions.EditSeasonsAndChallenges)]
public static partial class CreateSeason
{
    public sealed record Command([property: FromBody] SaveSeasonCommand Body);

    private static async ValueTask<Results<Ok<SeasonPreviewDto>, BadRequest<string>>> HandleAsync(
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

        var numberExists = await earthDb.Seasons
            .AsNoTracking()
            .AnyAsync(season => season.SeasonNumber == body.SeasonNumber, cancellationToken);

        if (numberExists)
        {
            return TypedResults.BadRequest($"Season #{body.SeasonNumber} already exists.");
        }

        await using var transaction = await earthDb.Database.BeginTransactionAsync(cancellationToken);

        var season = new SeasonEF
        {
            Id = Guid.NewGuid(),
            SeasonNumber = body.SeasonNumber,
            StartDate = null,
            EndDate = null,
            IsActive = false,
            Version = 1,
            ExclusiveRewards = body.ExclusiveRewards is not null ? SeasonUtils.MapRewardsToDb(body.ExclusiveRewards) : null,
            Challenges = [],
        };

        foreach (var challengeDto in body.Challenges)
        {
            var objectiveType = SeasonUtils.MapObjectiveTypeToDb(challengeDto.ObjectiveType);

            season.Challenges.Add(new ChallengeDefinitionEF
            {
                Id = challengeDto.Id,
                SeasonId = season.Id,
                PrerequisiteIds = [.. challengeDto.PrerequisiteIds.Distinct()],
                PrerequisiteLogicalCondition = challengeDto.PrerequisiteIsOr ? LogicalConditionEF.Or : LogicalConditionEF.And,
                Order = challengeDto.Order,
                TotalThreshold = challengeDto.TotalThreshold,
                ObjectiveType = objectiveType,
                ObjectiveTarget = challengeDto.ObjectiveTarget,
                IsFinalSeasonChallenge = challengeDto.IsFinalSeasonChallenge || objectiveType is ObjectiveTypeEF.SeasonCapstone,
                Rewards = SeasonUtils.MapRewardsToDb(challengeDto.Rewards),
            });
        }

        earthDb.Seasons.Add(season);

        await SeasonUtils.ReplaceSeasonTranslationsAsync(earthDb, season.SeasonNumber, season.SeasonNumber, [], body, cancellationToken);
        await SeasonUtils.UpsertSeasonTexturesAsync(earthDb, season.SeasonNumber, season.SeasonNumber, body.TextureOverrides, cancellationToken);

        await earthDb.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        await eventBus.PublishAsync("seasons", "seasons_data_updated", string.Empty, cancellationToken);
        await eventBus.PublishAsync("resourcepack", "synthesize", string.Empty, cancellationToken);

        var dto = await SeasonUtils.ToPreviewDtoAsync(earthDb, season, cancellationToken);
        return TypedResults.Ok(dto);
    }
}
