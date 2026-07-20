using System.Diagnostics;
using Immediate.Apis.Shared;
using Immediate.Handlers.Shared;
using Microsoft.AspNetCore.Http.HttpResults;
using Solace.Common.Asp.Auth;
using Solace.Common.Asp.Json;

namespace Solace.AuthServer.Features.PlayfabApi.Client;

[Handler]
[MapPost("Client/GetUserPublisherData")]
[MapGroup<PlayfabApiGroup>]
public sealed partial class GetUserPublisherData(
    CryptoSecrets cryptoSecrets,
    IHttpContextAccessor httpContextAccessor,
    ILogger<GetUserPublisherData> logger
)
{
    [ForcePascalCase]
    public sealed record Command(
        RequestEntity Entity,
        string[] Keys
    );

    [ForcePascalCase]
    public sealed record Response(
        PublisherData Data,
        int DataVersion
    );

    [ForcePascalCase]
    public sealed record PublisherData(
        PlayFabCommerceEnabled PlayFabCommerceEnabled
    );

    [ForcePascalCase]
    public sealed record PlayFabCommerceEnabled(
        string Value,
        string LastUpdated,
        string Permission
    );

    private async ValueTask<Results<Ok<OkResponse<Response>>, ForbidHttpResult, BadRequest>> HandleAsync(
        Command command,
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

        switch (command.Entity.Type)
        {
            case "master_player_account":
                {
                    return TypedResults.Ok(new OkResponse<Response>(
                        200,
                        "OK",
                        new Response(
                            new PublisherData(
                                new PlayFabCommerceEnabled(
                                    "true",
                                    "2019-12-01T00:00:00Z",
                                    "Public"
                                )
                            ),
                            1
                        )
                    ));
                }

            default:
                return TypedResults.BadRequest();
        }
    }
}