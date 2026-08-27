using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Solace.ApiServer.Models;
using Solace.ApiServer.Utils;

namespace Solace.ApiServer;

[ApiController]
internal abstract class LoginServerControllerBase : SolaceControllerBase
{
    protected LoginServerControllerBase(CryptoSecrets cryptoSecrets)
    {
        CryptoSecrets = cryptoSecrets;
    }

    protected CryptoSecrets CryptoSecrets { get; }

    protected Union<Tokens.Xbox.XapiToken, Results<UnauthorizedHttpResult, BadRequest>> XboxLiveAuth()
    {
        var authorization = XboxAuthorizationUtils.Parse(Request.Headers["Authorization"].FirstOrDefault());

        if (authorization is not { } authValue)
        {
            return (Results<UnauthorizedHttpResult, BadRequest>)TypedResults.BadRequest();
        }

        var token = JwtUtils.Verify<Tokens.Xbox.XapiToken>(authValue.TokenString, CryptoSecrets.LiveXapiTokenSecret)?.Data;

        if (token is null || token.UserId != authValue.UserId)
        {
            return (Results<UnauthorizedHttpResult, BadRequest>)TypedResults.Unauthorized();
        }

        return token;
    }

    protected Union<Tokens.Playfab.EntityToken, Results<ForbidHttpResult, BadRequest>> PlayfabAuth()
    {
        if (!Request.Headers.TryGetValue("X-EntityToken", out var tokenString) || tokenString.Count < 1)
        {
            return (Results<ForbidHttpResult, BadRequest>)TypedResults.BadRequest();
        }

        var token = JwtUtils.Verify<Tokens.Playfab.EntityToken>(tokenString[0] ?? "", CryptoSecrets.PlayfabEntityTokenSecret)?.Data;
        if (token is null)
        {
            return (Results<ForbidHttpResult, BadRequest>)TypedResults.Forbid();
        }

        return token;
    }
}