using Immediate.Apis.Shared;
using Immediate.Handlers.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Solace.WebPortal.Common;

namespace Solace.WebPortal.Features.Data;

[Handler]
[MapGet("export")]
[MapGroup<DataGroup>]
[Authorize(Policy = Permissions.ExportData)]
public sealed partial class ExportData(DataArchiveService archiveService)
{
    public sealed record Query;

    private async ValueTask<FileStreamHttpResult> HandleAsync(Query _, CancellationToken cancellationToken)
    {
        var archive = await archiveService.ExportAsync(cancellationToken);
        return TypedResults.File(archive, "application/zip", "solace-data-export.zip");
    }
}
