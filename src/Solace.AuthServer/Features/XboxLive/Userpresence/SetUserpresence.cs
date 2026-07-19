using System.Diagnostics;
using System.Text.Json.Serialization;
using Immediate.Apis.Shared;
using Immediate.Handlers.Shared;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Solace.AuthServer.Features.XboxLive.Userpresence;

[Handler]
[MapPost("userpresence.xboxlive.com/users/{XuidParam}/devices/current/titles/current")]
public sealed partial class SetUserpresence(
    IHttpContextAccessor httpContextAccessor,
    CryptoSecrets cryptoSecrets,
    ILogger<SetUserpresence> logger
)
{
    public sealed record Command
    {
        [JsonNamingPolicy(JsonKnownNamingPolicy.CamelCase)]
        public sealed record CommandBody(
            Activity? Activity,
            string State
        );

        [FromRoute]
        public required string XuidParam { get; init; }

        [FromBody]
        public required CommandBody Body { get; init; }
    }

    [JsonNamingPolicy(JsonKnownNamingPolicy.CamelCase)]
    public sealed record Activity(
        RichPresence RichPresence
    );

    [JsonNamingPolicy(JsonKnownNamingPolicy.CamelCase)]
    public sealed record RichPresence(
        string Id,
        Guid Scid
    );

    private async ValueTask<Results<Ok, UnauthorizedHttpResult, BadRequest>> HandleAsync(
       Command command,
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

        var xuidMatch = XuidUtils.GetXuidRegex().Match(command.XuidParam);

        var xuidString = xuidMatch.Success ? xuidMatch.Groups[1].Value : null;

        if (xuidString is null || !Guid.TryParse(xuidString, out var xuid))
        {
            return TypedResults.BadRequest();
        }

        if (xuid != token.UserId)
        {
            return TypedResults.Unauthorized();
        }

        // TODO

        return TypedResults.Ok();
    }
}