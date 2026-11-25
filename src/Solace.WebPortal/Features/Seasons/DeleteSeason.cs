using Immediate.Apis.Shared;
using Immediate.Handlers.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Solace.Db.Earth;
using Solace.EventBus.Client;
using Solace.WebPortal.Common;

namespace Solace.WebPortal.Features.Seasons;

[Handler]
[MapDelete("{seasonId:guid}")]
[MapGroup<SeasonsGroup>]
[Authorize(Policy = Permissions.EditSeasonsAndChallenges)]
public static partial class DeleteSeason
{
    public sealed record Command([property: FromRoute] Guid SeasonId);

    private static async ValueTask<Results<Ok, NotFound>> HandleAsync(
        Command command,
        EarthDbContext earthDb,
        EventBusClient eventBus,
        CancellationToken cancellationToken
    )
    {
        await using var transaction = await earthDb.Database.BeginTransactionAsync(cancellationToken);

        var season = await earthDb.Seasons
            .AsTracking()
            .Include(season => season.Challenges)
            .FirstOrDefaultAsync(season => season.Id == command.SeasonId, cancellationToken);

        if (season is null)
        {
            return TypedResults.NotFound();
        }

        var keysToRemove = new HashSet<string>(StringComparer.Ordinal);
        SeasonUtils.AddSeasonTranslationKeys(keysToRemove, season.SeasonNumber);
        foreach (var challenge in season.Challenges)
        {
            keysToRemove.Add($"challenge_{challenge.Id}.desc.single");
            keysToRemove.Add($"challenge_{challenge.Id}.desc.multiple");
        }

        var translations = await earthDb.ResourcePackTranslations
            .AsTracking()
            .Where(translation => keysToRemove.Contains(translation.Key))
            .ToListAsync(cancellationToken);

        if (translations.Count > 0)
        {
            earthDb.ResourcePackTranslations.RemoveRange(translations);
        }

        var assetPrefix = $"textures/ui/seasons/season_{season.SeasonNumber}/";
        var assets = await earthDb.ResourcePackAssets
            .AsTracking()
            .Where(asset => EF.Functions.Like(asset.Path, $"{assetPrefix}%"))
            .ToListAsync(cancellationToken);

        if (assets.Count > 0)
        {
            earthDb.ResourcePackAssets.RemoveRange(assets);
        }

        earthDb.Seasons.Remove(season);

        await earthDb.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        await eventBus.PublishAsync("seasons", "seasons_data_updated", string.Empty, cancellationToken);
        await eventBus.PublishAsync("resourcepack", "synthesize", string.Empty, cancellationToken);

        return TypedResults.Ok();
    }
}
