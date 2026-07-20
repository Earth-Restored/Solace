using System.Diagnostics;
using Immediate.Apis.Shared;
using Immediate.Handlers.Shared;
using Microsoft.AspNetCore.Http.HttpResults;
using Solace.Common.Asp;
using Solace.Common.Asp.Auth;
using Solace.Common.Asp.Json;

namespace Solace.AuthServer.Features.PlayfabApi.Client;

[Handler]
[MapPost("Client/GetPlayerStatistics")]
[MapGroup<PlayfabApiGroup>]
public sealed partial class GetPlayerStatistics(
    CryptoSecrets cryptoSecrets,
    IHttpContextAccessor httpContextAccessor,
    ILogger<GetPlayerStatistics> logger
)
{
    [ForcePascalCase]
    public sealed record Query(
        string[] StatisticNames
    );

    [ForcePascalCase]
    public sealed record Response(
        IEnumerable<Statistic> Statistics
    );

    [ForcePascalCase]
    public sealed record Statistic(
        string StatisticName,
        long Value
    );

    private async ValueTask<Results<Ok<OkResponse<Response>>, ForbidHttpResult, BadRequest>> HandleAsync(
        Query query,
        CancellationToken cancellationToken
    )
    {
        var httpContext = httpContextAccessor.HttpContext;
        Debug.Assert(httpContext is not null);

        if (!httpContext.Request.Headers.TryGetValue("X-Authorization", out var tokenHeader) || tokenHeader.Count < 1)
        {
            return TypedResults.BadRequest();
        }

        var tokenMatch = ClientUtils.GetAuthRegex().Match(tokenHeader[0] ?? "");

        var tokenString = tokenMatch.Success ? tokenMatch.Groups[1].Value : null;

        if (tokenString is null)
        {
            return TypedResults.BadRequest();
        }

        var token = JwtUtils.Verify<PlayfabSessionTicket>(tokenString, cryptoSecrets.PlayfabSessionTicketSecret, logger);
        if (token is null)
        {
            return TypedResults.Forbid();
        }

        // TODO
        var statistics = new Dictionary<string, long>()
        {
            ["BlocksPlaced"] = 0,
            ["BlocksCollected"] = 0,
            ["Deaths"] = 0,
            ["ItemsCrafted"] = 0,
            ["ItemsSmelted"] = 0,
            ["ToolsBroken"] = 0,
            ["MobsKilled"] = 0,
            ["BuildplateSeconds"] = 0,
            ["SharedBuildplateViews"] = 0,
            ["AdventuresPlayed"] = 0,
            ["TappablesCollected"] = 0,
            ["MobsCollected"] = 0,
            ["ChallengesCompleted"] = 0,
        };

        return TypedResults.Ok(new OkResponse<Response>(
            200,
            "OK",
            new Response(
                query.StatisticNames
                    .Where(statistics.ContainsKey)
                    .Select(field => new Statistic(field, statistics[field])
                    {
                        StatisticName = field,
                        Value = statistics[field],
                    })
            )
        ));
    }
}