using Immediate.Apis.Shared;
using Immediate.Handlers.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Solace.Db.Earth;
using Solace.Db.Earth.Models.Player;
using Solace.WebPortal.Common;
using Solace.WebPortal.Common.Features.Common;
using Solace.WebPortal.Common.Features.Players.Buildplates;

namespace Solace.WebPortal.Features.Players.Buildplates;

[Handler]
[MapGet("")]
[MapGroup<BuildplatesGroup>]
[Authorize]
public static partial class GetBuildplates
{
    public sealed record Query(
        [property: FromRoute] Guid PlayerId,
        string? SearchTerm = null,
        int Page = 1,
        int PageSize = 8
    ) : PagedSearchQuery(SearchTerm, Page, PageSize);

    private static async ValueTask<Results<Ok<PagedSearchResult<List<BuildplateDto>>>, NotFound, UnauthorizedHttpResult, ForbidHttpResult>> HandleAsync(
        Query query,
        EarthDbContext earthDb,
        IHttpContextAccessor httpContextAccessor,
        CancellationToken cancellationToken)
    {
        var httpUser = httpContextAccessor.HttpContext?.User;
        if (httpUser is null)
        {
            return TypedResults.Unauthorized();
        }

        var userId = httpUser.GetIdLong();

        if (!httpUser.HasPermission(Permissions.ViewPlayers))
        {
            var profile = await earthDb.Profiles
               .AsNoTracking()
               .Select(profile => new { profile.Id, profile.WebPortalAccountId, })
               .FirstOrDefaultAsync(profile => profile.Id == query.PlayerId, cancellationToken);

            if (profile is null)
            {
                return TypedResults.NotFound();
            }

            if (profile.WebPortalAccountId != userId)
            {
                return TypedResults.Forbid();
            }
        }

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

        var items = await dbQuery
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(buildplate => new BuildplateDto(
                buildplate.Id,
                buildplate.TemplateId,
                buildplate.Name,
                buildplate.BlocksPerMeter,
                buildplate.Size,
                buildplate.Offset,
                buildplate.Night,
                buildplate.ServerDataObjectId,
                buildplate.PreviewObjectId))
            .ToListAsync(cancellationToken);

        return TypedResults.Ok(new PagedSearchResult<List<BuildplateDto>>(items, totalCount, matchingCount));
    }
}
