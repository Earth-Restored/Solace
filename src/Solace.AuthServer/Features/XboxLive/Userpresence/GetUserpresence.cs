using System.Diagnostics;
using System.Globalization;
using System.Text.Json.Serialization;
using Immediate.Apis.Shared;
using Immediate.Handlers.Shared;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Solace.Common.Asp.Auth;

namespace Solace.AuthServer.Features.XboxLive.Userpresence;

[Handler]
[MapGet("userpresence.xboxlive.com/users/{XuidParam}")]
public sealed partial class GetUserpresence(
    IHttpContextAccessor httpContextAccessor,
    CryptoSecrets cryptoSecrets,
    ILogger<GetUserpresence> logger
)
{
    public sealed record Query
    {
        [FromRoute]
        public required string XuidParam { get; init; }
    }

    [JsonNamingPolicy(JsonKnownNamingPolicy.CamelCase)]
    public sealed record Response(
        Guid Xuid,
        string State,
        Device[] Devices
    );

    [JsonNamingPolicy(JsonKnownNamingPolicy.CamelCase)]
    public sealed record Device(
        string Type,
        Title[] Titles
    );

    [JsonNamingPolicy(JsonKnownNamingPolicy.CamelCase)]
    public sealed record Title(
        string Id,
        string Name,
        string Placement,
        string State,
        string LastModified
    );

    private async ValueTask<Results<Ok<Response>, UnauthorizedHttpResult, BadRequest>> HandleAsync(
       Query query,
       CancellationToken cancellationToken)
    {
        var httpContext = httpContextAccessor.HttpContext;
        Debug.Assert(httpContext is not null);

        var authUnion = AuthUtils.XboxLiveAuth(httpContext.Request, cryptoSecrets, logger);
        if (authUnion is not XapiToken)
        {
            var results = (Results<UnauthorizedHttpResult, BadRequest>)authUnion.Value!;
            return results.Result is UnauthorizedHttpResult unauthorized ? unauthorized : (BadRequest)results.Result;
        }

        var xuidMatch = XuidUtils.GetXuidRegex().Match(query.XuidParam);

        var xuidString = xuidMatch.Success ? xuidMatch.Groups["xuid"].Value : null;

        if (xuidString is null || !Guid.TryParse(xuidString, out var xuid))
        {
            return TypedResults.BadRequest();
        }

        return TypedResults.Ok(new Response(
            xuid,
            "Online",
            [
                new Device(
                    "Android",
                    [
                        new Title(
                            "2037747551",
                            "",
                            "Full",
                            "Active",
                            DateTime.UtcNow.Subtract(TimeSpan.FromHours(1)).ToString("yyyy-MM-ddTHH:mm:ss.fffffff", CultureInfo.InvariantCulture)
                        ),
                    ]
                ),
            ]
        ));
    }
}