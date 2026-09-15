using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Solace.ApiServer.Types.Common;
using Solace.ApiServer.Utils;
using Rewards = Solace.ApiServer.Types.Common.Rewards;

namespace Solace.ApiServer.Controllers;

[Authorize]
[ApiVersion("1.1")]
[Route("1/api/v{version:apiVersion}/player/challenges")]
[ApiController]
internal sealed class ChallengesController : ControllerBase
{
    internal sealed record ChallengeRecord(
        string ReferenceId,
        string? ParentId,
        string GroupId,
        string Duration,
        string Type,
        string Category,
        Rarity? Rarity,
        int Order,
        string EndTimeUtc,
        string State,
        bool IsComplete,
        int PercentComplete,
        int CurrentCount,
        int TotalThreshold,
        string[] PrerequisiteIds,
        string PrerequisiteLogicalCondition,
        Rewards Rewards,
        object ClientProperties
    );

    internal sealed record ChallengesResponse(
        Dictionary<Guid, ChallengeRecord> Challenges,
        Guid ActiveSeasonChallenge
    );

    [HttpGet]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Endpoints cannot be static")]
    public EarthApiResponse<ChallengesResponse> Get()
    {
        // TODO: this is currently just a stub required for the journal to load properly in the client

        var guid1 = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var guid2 = Guid.Parse("00000000-0000-0000-0000-000000000002");

        return new EarthApiResponse<ChallengesResponse>(new ChallengesResponse(
            new Dictionary<Guid, ChallengeRecord>()
            {
                { guid1, new ChallengeRecord(
                    "00000000-0000-0000-0000-000000000001",
                    null,
                    "00000000-0000-0000-0000-000000000001",
                    "Season",
                    "Regular",
                    "season_1",
                    null,
                    0,
                    TimeFormatter.FormatTime(DateTimeOffset.UtcNow.AddDays(1)),
                    "Locked",
                    false,
                    0,
                    0,
                    1,
                    [],
                    "And",
                    new Rewards(null, null, null, [], [], [], ["230f5996-04b2-4f0e-83e5-4056c7f1d946"], []),
                    new object()
                ) },
                { guid2, new ChallengeRecord(
                   "00000000-0000-0000-0000-000000000002",
                    null,
                    "00000000-0000-0000-0000-000000000001",
                    "Season",
                    "Regular",
                    "season_1",
                    null,
                    0,
                    TimeFormatter.FormatTime(DateTimeOffset.UtcNow.AddDays(1)),
                    "Locked",
                    false,
                    0,
                    0,
                    1,
                    [],
                    "And",
                    new Rewards(null, null, null, [], [], [], ["d7725840-4376-44fc-9220-585f45775371"], []),
                    new object()
                ) }
            },
            Guid.Empty)
        );
    }
}
