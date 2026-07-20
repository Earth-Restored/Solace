using Immediate.Apis.Shared;
using Immediate.Handlers.Shared;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Solace.AuthServer.Features.PlayfabApi.Login;

[Handler]
[MapPost("Client/LinkXboxAccount")]
[MapGroup<PlayfabApiGroup>]
public static partial class LinkXboxAccount
{
    public sealed record Command;

    private static async ValueTask<Ok<ErrorResponse>> HandleAsync(
        Command _,
        CancellationToken cancellationToken
    )
        => TypedResults.Ok(new ErrorResponse(
            401,
            "Unauthorized",
            "NotAuthenticated",
            1074,
            "This API method does not allow anonymous callers.",
            null
        ));
}