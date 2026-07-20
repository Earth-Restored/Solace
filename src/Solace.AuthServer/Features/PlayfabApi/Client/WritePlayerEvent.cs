using System.Diagnostics;
using Immediate.Apis.Shared;
using Immediate.Handlers.Shared;
using Microsoft.AspNetCore.Http.HttpResults;
using Solace.AuthServer.Features.Common;
using Solace.Common.Asp;
using Solace.Common.Asp.Auth;
using Solace.Common.Asp.Json;

namespace Solace.AuthServer.Features.PlayfabApi.Client;

[Handler]
[MapPost("Client/WritePlayerEvent")]
[MapGroup<PlayfabApiGroup>]
public sealed partial class WritePlayerEvent(
    CryptoSecrets cryptoSecrets,
    IHttpContextAccessor httpContextAccessor,
    ILogger<WritePlayerEvent> logger
)
{
    [ForcePascalCase]
    public sealed record Command(
        Payload Body,
        string EventName,
        object? Timestamp
    );

    [ForcePascalCase]
    public sealed record Payload(
        object? Measurements,
        Dictionary<string, object> Properties
    );

    [ForcePascalCase]
    public sealed record Response(
        string EventId
    );

    private async ValueTask<Results<Ok<OkResponse<Response>>, ForbidHttpResult, BadRequest>> HandleAsync(
        Command _,
        CancellationToken cancellationToken
    )
    {
        var httpContext = httpContextAccessor.HttpContext;
        Debug.Assert(httpContext is not null);

        if (!httpContext.Request.Headers.TryGetValue("X-Authorization", out var tokenHeader) || tokenHeader.Count < 1)
        {
            return TypedResults.BadRequest();
        }

        var tokenMatch = ClientUtils.GetAuthRegex().Match(tokenHeader[0] ?? "");

        var tokenString = tokenMatch.Success ? tokenMatch.Groups[1].Value : null;

        if (tokenString is null)
        {
            return TypedResults.BadRequest();
        }

        var token = JwtUtils.Verify<PlayfabSessionTicket>(tokenString, cryptoSecrets.PlayfabSessionTicketSecret, logger);
        if (token is null)
        {
            return TypedResults.Forbid();
        }

        return TypedResults.Ok(new OkResponse<Response>(
            200,
            "OK",
            new Response(Guid.CreateVersion7().ToString("N"))
        ));
    }
}