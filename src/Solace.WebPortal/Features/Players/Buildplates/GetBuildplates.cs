using Immediate.Apis.Shared;
using Immediate.Handlers.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Solace.Common.Asp;
using Solace.Common.Utils;
using Solace.Db.Earth;
using Solace.Db.Earth.Models.Player;
using Solace.EventBus.Client;
using Solace.ObjectStore.Client;
using Solace.StaticData;
using Solace.WebPortal.Common;
using Solace.WebPortal.Common.Features.Common;
using Solace.WebPortal.Common.Features.Players.Buildplates;

namespace Solace.WebPortal.Features.Players.Buildplates;

[Handler]
[MapGet("")]
[MapGroup<BuildplatesGroup>]
[Authorize]
public sealed partial class GetBuildplates(
    EarthDbContext earthDb,
    EventBusClient eventBus,
    ObjectStoreClient objectStore,
    StaticDataProvider staticData,
    IHttpContextAccessor httpContextAccessor,
    ILogger<GetBuildplates> logger
)
{
    public sealed record Query(
        [property: FromRoute] Guid PlayerId,
        string? SearchTerm = null,
        int Page = 1,
        int PageSize = 8
    ) : PagedSearchQuery(SearchTerm, Page, PageSize);

    private async ValueTask<Results<Ok<PagedSearchResult<IEnumerable<BuildplateDto>>>, NotFound, UnauthorizedHttpResult, ForbidHttpResult>> HandleAsync(
        Query query,
        CancellationToken cancellationToken)
    {
        var httpUser = httpContextAccessor.HttpContext?.User;
        if (httpUser is null)
        {
            return TypedResults.Unauthorized();
        }

        var userId = httpUser.GetIdLong();

        int currentLevel;
        if (!httpUser.HasPermission(Permissions.ViewPlayers))
        {
            var profile = await earthDb.Profiles
               .AsNoTracking()
               .Select(profile => new { profile.Id, profile.WebPortalAccountId, profile.Level, })
               .FirstOrDefaultAsync(profile => profile.Id == query.PlayerId, cancellationToken);

            if (profile is null)
            {
                return TypedResults.NotFound();
            }

            if (profile.WebPortalAccountId != userId)
            {
                return TypedResults.Forbid();
            }

            currentLevel = profile.Level;
        }
        else
        {
            var currentLevelNullable = await earthDb.Profiles
                .AsNoTracking()
                .Where(profile => profile.Id == query.PlayerId)
                .Select(profile => (int?)profile.Level)
                .FirstOrDefaultAsync(cancellationToken);

            if (currentLevelNullable is null)
            {
                return TypedResults.NotFound();
            }

            currentLevel = currentLevelNullable.Value;
        }

        await LevelBuildplateSeeder.SeedLevelBuildplates(query.PlayerId, earthDb, eventBus, objectStore, staticData.Buildplates, logger, cancellationToken);

        var dbQuery = (IQueryable<PlayerBuildplateEF>)earthDb.PlayerBuildplates
            .AsNoTracking()
            .Where(buildplate => buildplate.ProfileId == query.PlayerId)
            .OrderBy(template => template.Name);

        var totalCount = await dbQuery.CountAsync(cancellationToken);
        int matchingCount;
        if (!string.IsNullOrWhiteSpace(query.SearchTerm))
        {
            var search = $"%{query.SearchTerm}%";
            dbQuery = dbQuery.Where(buildplate =>
                EF.Functions.ILike(buildplate.Name, search) ||
                EF.Functions.ILike(buildplate.Id.ToString(), search));

            matchingCount = await dbQuery.CountAsync(cancellationToken);
        }
        else
        {
            matchingCount = totalCount;
        }

        IEnumerable<BuildplateDto> buildplates = await dbQuery
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(buildplate => new BuildplateDto(
                buildplate.Id,
                buildplate.TemplateId,
                buildplate.Name,
                buildplate.BlocksPerMeter,
                buildplate.Size,
                buildplate.Offset,
                null,
                false,
                buildplate.Night,
                buildplate.ServerDataObjectId,
                buildplate.PreviewObjectId))
            .ToListAsync(cancellationToken);

        var templateIds = buildplates
            .Select(buildplate => buildplate.TemplateId)
            .WhereNotNull()
            .ToHashSet();

        var templates = await earthDb.TemplateBuildplates
            .AsNoTracking()
            .Where(template => templateIds.Contains(template.Id))
            .Select(template => new { template.Id, template.RequiredLevel, template.Order, })
            .ToDictionaryAsync(template => template.Id, cancellationToken);

        buildplates = buildplates.Select(buildplate =>
        {
            if (buildplate.TemplateId is null || !templates.TryGetValue(buildplate.TemplateId.Value, out var template))
            {
                return buildplate;
            }

            return buildplate with
            {
                RequiredLevel = template.RequiredLevel,
                Locked = currentLevel < (template?.RequiredLevel ?? 1),
            };
        });

        return TypedResults.Ok(new PagedSearchResult<IEnumerable<BuildplateDto>>(buildplates, totalCount, matchingCount));
    }
}
