using Immediate.Apis.Shared;
using Immediate.Handlers.Shared;
using Microsoft.AspNetCore.Http.HttpResults;
using Solace.Common.Asp.Json;

namespace Solace.AuthServer.Features.PlayfabApi.Login;

[Handler]
[MapPost("Client/LoginWithCustomID")]
[MapGroup<PlayfabApiGroup>]
public static partial class LoginWithCustomID
{
    [ForcePascalCase]
    public sealed record Command(
        string TitleId,
        object? EncryptedRequest,
        object? PlayerSecret,
        bool CreateAccount,
        string CustomId
    );

    private static async ValueTask<Results<Ok<ErrorResponse>, BadRequest>> HandleAsync(
        Command command,
        CancellationToken cancellationToken
    )
    {
        if (!PlayfabApiUtils.GetTitleIdRegex().IsMatch(command.TitleId))
        {
            return TypedResults.BadRequest();
        }

        return TypedResults.Ok(new ErrorResponse(
            403,
            "Forbidden",
            "NotAuthorizedByTitle",
            1191,
            "Action not authorized by title",
            null
        ));
    }
}