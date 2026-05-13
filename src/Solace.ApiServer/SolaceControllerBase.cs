using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using Solace.ApiServer.Models;
using Solace.ApiServer.Utils;
using System.Security.Claims;

namespace Solace.ApiServer;

[ApiController]
internal abstract class SolaceControllerBase : ControllerBase
{
    private static Config Config => Program.config;

    // todo: JsonHttpResult<EarthApiResponse>
    protected static ContentHttpResult EarthJson(object results)
        => JsonCamelCase(new EarthApiResponse(results));

    // todo: JsonHttpResult<EarthApiResponse>
    protected static ContentHttpResult EarthJson(object? results, EarthApiResponse.UpdatesResponse? updates)
        => JsonCamelCase(new EarthApiResponse(results, updates));

    protected static ContentHttpResult JsonCamelCase(object value)
        => TypedResults.Content(Common.Json.Serialize(value), "application/json");

    protected static ContentHttpResult JsonPascalCase(object value)
        => TypedResults.Content(JsonSerializer.Serialize(value), "application/json");

    protected bool TryGetAccountId(out Guid accountId)
    {
        string? playerIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(playerIdString))
        {
            accountId = default;
            return false;
        }

        return Guid.TryParse(playerIdString, out accountId);
    }

    protected Union<Tokens.Xbox.XapiToken, Results<UnauthorizedHttpResult, BadRequest>> XboxLiveAuth()
    {
        var authorization = XboxAuthorizationUtils.Parse(Request.Headers["Authorization"].FirstOrDefault());

        if (authorization is not { } authValue)
        {
            return (Results<UnauthorizedHttpResult, BadRequest>)TypedResults.BadRequest();
        }

        var token = JwtUtils.Verify<Tokens.Xbox.XapiToken>(authValue.TokenString, Config.XboxLive.XapiTokenSecretBytes)?.Data;

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

        var token = JwtUtils.Verify<Tokens.Playfab.EntityToken>(tokenString[0] ?? "", Config.PlayfabApi.EntityTokenSecretBytes)?.Data;
        if (token is null)
        {
            return (Results<ForbidHttpResult, BadRequest>)TypedResults.Forbid();
        }

        return token;
    }
}
