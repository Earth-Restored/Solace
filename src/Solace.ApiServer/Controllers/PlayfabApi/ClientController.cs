using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using System.Text.RegularExpressions;
using Solace.ApiServer.Models;
using Solace.ApiServer.Models.Playfab;
using Solace.ApiServer.Utils;
using Solace.Common.Utils;
using Solace.DB;
using ActivityLog = Solace.DB.Models.Player.ActivityLog;
using ChallengeProgressVersion = Solace.ApiServer.Utils.ChallengeProgressVersion;
using Journal = Solace.DB.Models.Player.Journal;

namespace Solace.ApiServer.Controllers.PlayfabApi;

[Route("Client")]
[Route("20CA2.playfabapi.com/Client")]
internal sealed partial class ClientController : SolaceControllerBase
{
    private static Config config => Program.config;

    private sealed record GetUserPublisherDataRequest(
        GetUserPublisherDataRequest.EntityR Entity,
        string[] Keys
    )
    {
        internal sealed record EntityR(
            string Id,
            string Type
        );
    }

    [HttpPost("GetUserPublisherData")]
    public async Task<Results<ContentHttpResult, ForbidHttpResult, BadRequest>> GetUserPublisherData()
    {
        var cancellationToken = Request.HttpContext.RequestAborted;

        var request = await Request.Body.AsJsonAsync<GetUserPublisherDataRequest>(cancellationToken);

        if (request is null)
        {
            return TypedResults.BadRequest();
        }

        if (!Request.Headers.TryGetValue("X-Authorization", out var tokenHeader) || tokenHeader.Count < 1)
        {
            return TypedResults.BadRequest();
        }

        Match tokenMatch = GetAuthRegex().Match(tokenHeader[0] ?? "");

        string? tokenString = tokenMatch.Success ? tokenMatch.Groups[1].Value : null;

        if (tokenString is null)
        {
            return TypedResults.BadRequest();
        }

        var token = JwtUtils.Verify<Tokens.Shared.PlayfabSessionTicket>(tokenString, config.PlayfabApi.SessionTicketSecretBytes);
        if (token is null)
        {
            return TypedResults.Forbid();
        }

        switch (request.Entity.Type)
        {
            case "master_player_account":
                {
                    var publisherData = new Dictionary<string, object>()
                    {
                        ["PlayFabCommerceEnabled"] = new Dictionary<string, string>()
                        {
                            ["Value"] = "true",
                            ["LastUpdated"] = "2019-12-01T00:00:00Z",
                            ["Permission"] = "Public",
                        },
                        ["DataVersion"] = 35,
                    };

                    return JsonPascalCase(new PlayfabOkResponse(
                        200,
                        "OK",
                        new Dictionary<string, object>()
                        {
                            ["Data"] = request.Keys
                                .Where(publisherData.ContainsKey)
                                .ToDictionary(field => field, field => publisherData[field]),
                            ["DataVersion"] = 35,
                        }
                    ));
                }

            default:
                return TypedResults.BadRequest();
        }
    }

    private sealed record GetPlayerStatisticsRequest(
        string[] StatisticNames
    );

    [HttpPost("GetPlayerStatistics")]
    public async Task<Results<ContentHttpResult, ForbidHttpResult, BadRequest>> GetPlayerStatistics()
    {
        var cancellationToken = Request.HttpContext.RequestAborted;

        var request = await Request.Body.AsJsonAsync<GetPlayerStatisticsRequest>(cancellationToken);

        if (request is null)
        {
            return TypedResults.BadRequest();
        }

        if (!Request.Headers.TryGetValue("X-Authorization", out var tokenHeader) || tokenHeader.Count < 1)
        {
            return TypedResults.BadRequest();
        }

        Match tokenMatch = GetAuthRegex().Match(tokenHeader[0] ?? "");

        string? tokenString = tokenMatch.Success ? tokenMatch.Groups[1].Value : null;

        if (tokenString is null)
        {
            return TypedResults.BadRequest();
        }

        var token = JwtUtils.Verify<Tokens.Shared.PlayfabSessionTicket>(tokenString, config.PlayfabApi.SessionTicketSecretBytes);
        if (token is null)
        {
            return TypedResults.Forbid();
        }

        EarthDB.Results playerResults = await new EarthDB.Query(false)
            .Get("activityLog", token.Data.UserId, typeof(ActivityLog))
            .Get("challenges", token.Data.UserId, typeof(ChallengeProgressVersion))
            .Get("journal", token.Data.UserId, typeof(Journal))
            .ExecuteAsync(Program.DB, cancellationToken);

        ActivityLog activityLog = playerResults.Get<ActivityLog>("activityLog");
        ChallengeProgressVersion challengeProgress = playerResults.Get<ChallengeProgressVersion>("challenges");
        Journal journal = playerResults.Get<Journal>("journal");

        var statistics = BuildPlayerStatistics(activityLog, challengeProgress, journal);

        string[] requestedStatistics = request.StatisticNames is { Length: > 0 }
            ? request.StatisticNames
            : [.. statistics.Keys];

        return JsonPascalCase(new PlayfabOkResponse(
            200,
            "OK",
            new Dictionary<string, object>()
            {
                ["Statistics"] = requestedStatistics
                    .Where(statistics.ContainsKey)
                    .Select(field => new
                    {
                        StatisticName = field,
                        Value = statistics[field],
                    }),
            }
        ));
    }

    private static Dictionary<string, int> BuildPlayerStatistics(
        ActivityLog activityLog,
        ChallengeProgressVersion challengeProgress,
        Journal journal)
    {
        const string MobReferenceId = "1d981b84-a03a-451d-82a6-9bfe0fc885fb";
        const string BestDefenseReferenceId = "06eb0e50-b18d-43e8-9aad-422203ffdf28";
        const string PettingZooReferenceId = "6d01c0d0-2ac9-4549-be82-acd7f5631950";
        const string Season17CommonMobPettingZooReferenceId = "ccc1836f-ccda-4f33-8a4d-c42d8d366255";

        int itemsCrafted = 0;
        int itemsSmelted = 0;
        int tappablesFromLog = 0;

        foreach (ActivityLog.Entry entry in activityLog.Entries)
        {
            switch (entry)
            {
                case ActivityLog.TappableEntry:
                    tappablesFromLog++;
                    break;
                case ActivityLog.CraftingCompletedEntry crafting:
                    itemsCrafted += CountRewardItems(crafting.Rewards);
                    break;
                case ActivityLog.SmeltingCompletedEntry smelting:
                    itemsSmelted += CountRewardItems(smelting.Rewards);
                    break;
            }
        }

        int blocksCollected = journal.Items.Values.Sum(item => Math.Max(0, item.AmountCollected));
        int tappablesCollected = Math.Max(challengeProgress.TappablesRedeemed, tappablesFromLog);
        int mobsCollected = new[]
        {
            challengeProgress.GetObjectiveProgress(MobReferenceId),
            challengeProgress.GetObjectiveProgress(BestDefenseReferenceId),
            challengeProgress.GetObjectiveProgress(PettingZooReferenceId),
            challengeProgress.GetObjectiveProgress(Season17CommonMobPettingZooReferenceId),
        }.Max();

        var statistics = new Dictionary<string, int>()
        {
            ["BlocksPlaced"] = 0,
            ["BlocksCollected"] = blocksCollected,
            ["Deaths"] = 0,
            ["ItemsCrafted"] = itemsCrafted,
            ["ItemsSmelted"] = itemsSmelted,
            ["ToolsBroken"] = 0,
            ["MobsKilled"] = 0,
            ["BuildplateSeconds"] = 0,
            ["SharedBuildplateViews"] = 0,
            ["AdventuresPlayed"] = 0,
            ["TappablesCollected"] = tappablesCollected,
            ["MobsCollected"] = mobsCollected,
            ["ChallengesCompleted"] = challengeProgress.ClaimedChallengeIds?.Count ?? 0,
        };

        return statistics;
    }

    private static int CountRewardItems(Solace.DB.Models.Common.Rewards rewards)
        => rewards.Items.Values.Sum(count => Math.Max(0, count ?? 0));

    [HttpPost("WritePlayerEvent")]
    public ContentHttpResult WritePlayerEvent()
        => JsonPascalCase(new PlayfabOkResponse(
            200,
            "OK",
            new Dictionary<string, object>()
            {
                ["EventId"] = Guid.NewGuid().ToString("N"),
            }
        ));

    [GeneratedRegex("^[0-9A-F]{16}-(.*)$")]
    private static partial Regex GetAuthRegex();
}
