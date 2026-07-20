using Immediate.Apis.Shared;
using Immediate.Handlers.Shared;
using Microsoft.AspNetCore.Http.HttpResults;
using Solace.Common.Asp.Json;

namespace Solace.AuthServer.Features.PlayfabApi.Events;

[Handler]
[MapPost("Event/WriteTelemetryEvents")]
[MapGroup<PlayfabApiGroup>]
public static partial class WriteTelemetryEvents
{
    [ForcePascalCase]
    public sealed record Command(
        TelemetryEvent[] Events
    );

    [ForcePascalCase]
    public sealed record TelemetryEvent(
        RequestEntity Entity,
        string EventNamespace,
        string Name,
        object? OriginalId,
        object? OriginalTimestamp,
        Payload Payload,
        object? PayloadJSON
    );

    [ForcePascalCase]
    public sealed record Payload(
        object? Measurements,
        Dictionary<string, object> Properties
    );

    [ForcePascalCase]
    public sealed record Response(
        IEnumerable<string> AssignedEventIds
    );

    private static async ValueTask<Ok<OkResponse<Response>>> HandleAsync(
        Command command,
        CancellationToken cancellationToken
    )
        => TypedResults.Ok(new OkResponse<Response>(
            200,
            "OK",
            new Response(command.Events.Select(_ => Guid.CreateVersion7().ToString("N")))
        ));
}