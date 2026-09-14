using Immediate.Apis.Shared;
using Immediate.Handlers.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Solace.WebPortal.Common;
using Solace.WebPortal.Common.Features.Data;

namespace Solace.WebPortal.Features.Data;

[Handler]
[MapPost("import")]
[MapGroup<DataGroup>]
[Authorize(Policy = Permissions.EditData)]
public sealed partial class ImportData(DataArchiveService archiveService)
{
    public sealed record Command
    {
        public required IFormFile File { get; init; }
        public DataConflictResolution ConflictResolution { get; init; } = DataConflictResolution.Ignore;
    }

    internal static void CustomizeEndpoint(RouteHandlerBuilder endpoint)
        => endpoint.WithMetadata(new RequestSizeLimitAttribute(1_000_000_000));

    private async ValueTask<Results<Ok, BadRequest<string>>> HandleAsync(Command command, CancellationToken cancellationToken)
    {
        if (!Path.GetExtension(command.File.FileName).Equals(".zip", StringComparison.OrdinalIgnoreCase))
        {
            return TypedResults.BadRequest("Only .zip archives can be imported.");
        }

        try
        {
            await using var stream = command.File.OpenReadStream();
            await archiveService.ImportAsync(stream, command.ConflictResolution, cancellationToken);
            return TypedResults.Ok();
        }
        catch (InvalidDataException exception)
        {
            return TypedResults.BadRequest(exception.Message);
        }
    }
}
