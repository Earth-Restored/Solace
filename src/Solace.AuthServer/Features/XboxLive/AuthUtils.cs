using Microsoft.AspNetCore.Http.HttpResults;
using Solace.AuthServer.Features.Common;
using Solace.Common.Asp.Auth;

namespace Solace.AuthServer.Features.XboxLive;

public static class AuthUtils
{
    public static XboxLiveAuthResult XboxLiveAuth(HttpRequest request, CryptoSecrets cryptoSecrets, ILogger logger)
    {
        var authorization = XboxAuthorizationUtils.Parse(request.Headers["Authorization"].FirstOrDefault());

        if (authorization is not { } authValue)
        {
            return (Results<UnauthorizedHttpResult, BadRequest>)TypedResults.BadRequest();
        }

        var token = JwtUtils.Verify<XapiToken>(authValue.TokenString, cryptoSecrets.LiveXapiTokenSecret, logger)?.Data;

        if (token is null || token.UserId != authValue.UserId)
        {
            return (Results<UnauthorizedHttpResult, BadRequest>)TypedResults.Unauthorized();
        }

        return token;
    }

    public readonly union XboxLiveAuthResult(XapiToken, Results<UnauthorizedHttpResult, BadRequest>);
}