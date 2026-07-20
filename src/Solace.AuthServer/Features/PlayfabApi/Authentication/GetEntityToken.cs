using System.Diagnostics;
using Immediate.Apis.Shared;
using Immediate.Handlers.Shared;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Options;
using Solace.Common.Asp;
using Solace.Common.Asp.Auth;
using Solace.Common.Asp.Json;

namespace Solace.AuthServer.Features.PlayfabApi.Authentication;

[Handler]
[MapPost("Authentication/GetEntityToken")]
[MapGroup<PlayfabApiGroup>]
public sealed partial class GetEntityToken(
    CryptoSecrets cryptoSecrets,
    IHttpContextAccessor httpContextAccessor,
    IOptions<AuthSettings> authSettingsOption,
    ILogger<GetEntityToken> logger
)
{
    [ForcePascalCase]
    public sealed record Command(
        RequestEntity Entity
    );

    [ForcePascalCase]
    public sealed record Response(
        string EntityToken,
        DateTime TokenExpiration,
        ResponseEntity Entity
    );

    private async ValueTask<Results<Ok<OkResponse<Response>>, ForbidHttpResult, BadRequest>> HandleAsync(
        Command command,
        CancellationToken cancellationToken
    )
    {
        var httpContext = httpContextAccessor.HttpContext;
        Debug.Assert(httpContext is not null);

        var tokenUnion = PlayfabApiUtils.PlayfabAuth(cryptoSecrets, httpContext.Request, logger);

        if (tokenUnion.IsB)
        {
            return tokenUnion.B.Result is ForbidHttpResult forbid ? forbid : (BadRequest)tokenUnion.B.Result;
        }

        var token = tokenUnion.A;

        switch (command.Entity.Type)
        {
            case "master_player_account":
                {
                    if (token.Type is not "title_player_account" || token.Id != command.Entity.Id)
                    {
                        return TypedResults.Forbid();
                    }

                    if (command.Entity.Id is null)
                    {
                        return TypedResults.BadRequest();
                    }

                    var entityTokenValidity = ValidityDatePair.Create(authSettingsOption.Value.EntityTokenValidityMinutes);
                    var entityToken = new EntityToken(command.Entity.Id.Value, command.Entity.Type);
                    var entityTokenSting = JwtUtils.Sign(entityToken, cryptoSecrets.PlayfabEntityTokenSecret, entityTokenValidity);

                    return TypedResults.Ok(new OkResponse<Response>(
                        200,
                        "OK",
                        new Response(
                            entityTokenSting,
                            entityTokenValidity.ExpiresDT,
                            new(
                               entityToken.Id,
                               entityToken.Type,
                               entityToken.Type
                            )
                        )
                    ));
                }

            default:
                return TypedResults.BadRequest();
        }
    }
}