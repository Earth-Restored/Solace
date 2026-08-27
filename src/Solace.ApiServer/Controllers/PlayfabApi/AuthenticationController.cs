using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Solace.ApiServer.Models;
using Solace.ApiServer.Models.Playfab;
using Solace.ApiServer.Utils;
using Solace.Common.Utils;

namespace Solace.ApiServer.Controllers.PlayfabApi;

[Route("Authentication")]
[Route("20CA2.playfabapi.com/Authentication")]
internal sealed class AuthenticationController : LoginServerControllerBase
{
    public AuthenticationController(CryptoSecrets cryptoSecrets) : base(cryptoSecrets)
    {
    }

    private static Config Config => Program.config;

    private sealed record GetEntityTokenRequest(
        GetEntityTokenRequest.EntityR Entity
    )
    {
        internal sealed record EntityR(
            Guid Id,
            string Type
        );
    }

    private sealed record GetEntityTokenResponse(
        string EntityToken,
        DateTime TokenExpiration,
        GetEntityTokenResponse.EntityR Entity
    )
    {
        internal sealed record EntityR(
            Guid Id,
            string Type,
            string TypeString
        );
    }

    [HttpPost("GetEntityToken")]
    public async Task<Results<ContentHttpResult, ForbidHttpResult, BadRequest>> GetEntityTokenAsync()
    {
        var cancellationToken = Request.HttpContext.RequestAborted;

        var request = await Request.Body.AsJsonAsync<GetEntityTokenRequest>(cancellationToken);

        if (request is null)
        {
            return TypedResults.BadRequest();
        }

        var tokenUnion = PlayfabAuth();

        if (tokenUnion.IsB)
        {
            return tokenUnion.B.Result is ForbidHttpResult forbid ? forbid : (BadRequest)tokenUnion.B.Result;
        }

        var token = tokenUnion.A;

        switch (request.Entity.Type)
        {
            case "master_player_account":
                {
                    if (token.Type is not "title_player_account" || token.Id != request.Entity.Id)
                    {
                        return TypedResults.Forbid();
                    }

                    var entityTokenValidity = ValidityDatePair.Create(Config.PlayfabApi.EntityTokenValidityMinutes);
                    var entityToken = new Tokens.Playfab.EntityToken(request.Entity.Id, request.Entity.Type);
                    string entityTokenSting = JwtUtils.Sign(entityToken, CryptoSecrets.PlayfabEntityTokenSecret, entityTokenValidity);

                    return JsonPascalCase(new PlayfabOkResponse(
                        200,
                        "OK",
                        new GetEntityTokenResponse(
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
