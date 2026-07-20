using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http.HttpResults;
using Solace.Common;
using Solace.Common.Asp.Auth;

namespace Solace.AuthServer.Features.PlayfabApi;

public static partial class PlayfabApiUtils
{
    [GeneratedRegex("^[0-9A-F]{5}$", RegexOptions.None, matchTimeoutMilliseconds: 200)]
    public static partial Regex GetTitleIdRegex();

    public static Guid MinecoinCurrencyId { get; } = Guid.Parse("ecd19d3c-7635-402c-a185-eb11cb6c6946");

    public static Guid RubyCurrencyId { get; } = Guid.Parse("8b77345d-6250-4321-b3c2-373468b39457");

    public static Union<EntityToken, Results<ForbidHttpResult, BadRequest>> PlayfabAuth(CryptoSecrets cryptoSecrets, HttpRequest request, ILogger logger)
    {
        if (!request.Headers.TryGetValue("X-EntityToken", out var tokenString) || tokenString.Count < 1)
        {
            return (Results<ForbidHttpResult, BadRequest>)TypedResults.BadRequest();
        }

        var token = JwtUtils.Verify<EntityToken>(tokenString[0] ?? "", cryptoSecrets.PlayfabEntityTokenSecret, logger)?.Data;
        if (token is null)
        {
            return (Results<ForbidHttpResult, BadRequest>)TypedResults.Forbid();
        }

        return token;
    }
}
