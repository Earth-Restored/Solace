using System.Diagnostics;
using Immediate.Apis.Shared;
using Immediate.Handlers.Shared;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Solace.Common.Asp;

namespace Solace.AuthServer.Features.XboxLive.Privacy;

[Handler]
[MapGet("privacy.xboxlive.com/users/{XuidParam}/people/avoid")]
public sealed partial class GetAvoidPeople(
    IHttpContextAccessor httpContextAccessor,
    CryptoSecrets cryptoSecrets,
    ILogger<GetAvoidPeople> logger
)
{
    public sealed record Query
    {
        [FromRoute]
        public required string XuidParam { get; init; }
    }

    private async ValueTask<Results<Ok<PeopleResponse>, UnauthorizedHttpResult, BadRequest>> HandleAsync(
       Query query,
       CancellationToken cancellationToken)
    {
        var httpContext = httpContextAccessor.HttpContext;
        Debug.Assert(httpContext is not null);

        var authUnion = AuthUtils.XboxLiveAuth(httpContext.Request, cryptoSecrets, logger);
        if (authUnion.IsB)
        {
            return authUnion.B.Result is UnauthorizedHttpResult unauthorized ? unauthorized : (BadRequest)authUnion.B.Result;
        }

        var token = authUnion.A;

        var xuidMatch = XuidUtils.GetXuidRegex().Match(query.XuidParam);

        var xuidString = xuidMatch.Success ? xuidMatch.Groups[1].Value : null;

        if (xuidString is null || !Guid.TryParse(xuidString, out var xuid))
        {
            return TypedResults.BadRequest();
        }

        if (xuid != token.UserId)
        {
            return TypedResults.Unauthorized();
        }

        return TypedResults.Ok(new PeopleResponse(
            []
        ));
    }
}