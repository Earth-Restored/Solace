using Immediate.Apis.Shared;
using Immediate.Handlers.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Solace.Db.Earth;
using Solace.WebPortal.Common;
using Solace.WebPortal.Common.Features.Buildplates;

namespace Solace.WebPortal.Features.Buildplates.Templates;

[Handler]
[MapPut("{id}")]
[MapGroup<TemplatesGroup>]
[Authorize(Policy = Permissions.ManageBuildplates)]
public static partial class UpdateTemplate
{
    private static async ValueTask<Results<NotFound, BadRequest<string>, NoContent>> HandleAsync(
        UpdateTemplateCommand command,
        EarthDbContext earthDb,
        CancellationToken ct)
    {
        var template = await earthDb.TemplateBuildplates.FirstOrDefaultAsync(x => x.Id == command.Id, ct);

        if (template is null)
        {
            return TypedResults.NotFound();
        }

        var name = command.Name.Trim();

        if (string.IsNullOrWhiteSpace(name))
        {
            return TypedResults.BadRequest("Name cannot be empty.");
        }

        template.Name = name;
        template.BlocksPerMeter = command.BlocksPerMeter;

        await earthDb.SaveChangesAsync(ct);
        return TypedResults.NoContent();
    }
}
