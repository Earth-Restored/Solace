using Immediate.Apis.Shared;
using Immediate.Handlers.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Solace.Db.Earth;
using Solace.Db.Earth.Models.Global;
using Solace.WebPortal.Common;
using Solace.WebPortal.Common.Features.Buildplates;

namespace Solace.WebPortal.Features.Buildplates.Templates;

[Handler]
[MapGet("")]
[MapGroup<TemplatesGroup>]
[Authorize(Policy = Permissions.ViewBuildplates)]
public static partial class GetTemplates
{
    public sealed record Query(string? SearchTerm, int Page, int PageSize);

    private static async ValueTask<PagedResult<BuildplateTemplateDto>> HandleAsync(
        Query query,
        EarthDbContext earthDb,
        CancellationToken ct)
    {
        var dbQuery = (IQueryable<TemplateBuildplateEF>)earthDb.TemplateBuildplates
            .AsNoTracking()
            .OrderBy(template => template.Name);

        if (!string.IsNullOrWhiteSpace(query.SearchTerm))
        {
            dbQuery = dbQuery.Where(b => EF.Functions.Like(b.Name, $"%{query.SearchTerm}%"));
        }

        var total = await dbQuery.CountAsync(ct);
        var items = await dbQuery
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(b => new BuildplateTemplateDto(b.Id, b.Name, b.BlocksPerMeter, b.Size, b.Offset, b.Night, b.ServerDataObjectId, b.PreviewObjectId))
            .ToListAsync(ct);

        return new PagedResult<BuildplateTemplateDto>(items, total);
    }
}
