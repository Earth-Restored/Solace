using Immediate.Apis.Shared;
using Immediate.Handlers.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Solace.Db.Earth;
using Solace.Db.Earth.Models.Global;
using Solace.WebPortal.Common;
using Solace.WebPortal.Common.Features.Buildplates;
using Solace.WebPortal.Common.Features.Common;

namespace Solace.WebPortal.Features.Buildplates.Templates;

[Handler]
[MapGet("")]
[MapGroup<TemplatesGroup>]
[Authorize(Policy = Permissions.ViewBuildplates)]
public static partial class GetTemplates
{
    private static async ValueTask<PagedSearchResult<List<BuildplateTemplateDto>>> HandleAsync(
        PagedSearchQuery query,
        EarthDbContext earthDb,
        CancellationToken cancellationToken)
    {
        var dbQuery = (IQueryable<TemplateBuildplateEF>)earthDb.TemplateBuildplates
            .AsNoTracking()
            .OrderBy(template => template.Name);

        var totalCount = await dbQuery.CountAsync(cancellationToken);
        int matchingCount;
        if (!string.IsNullOrWhiteSpace(query.SearchTerm))
        {
            var search = $"%{query.SearchTerm}%";
            dbQuery = dbQuery.Where(buildplate => EF.Functions.ILike(buildplate.Name, search));

            matchingCount = await dbQuery.CountAsync(cancellationToken);
        }
        else
        {
            matchingCount = totalCount;
        }

        var items = await dbQuery
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(b => new BuildplateTemplateDto(b.Id, b.Name, b.BlocksPerMeter, b.Size, b.Offset, b.Night, b.ServerDataObjectId, b.PreviewObjectId))
            .ToListAsync(cancellationToken);

        return new(items, totalCount, matchingCount);
    }
}
