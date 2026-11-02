using System.Globalization;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Solace.ApiServer.Types.Common;
using Solace.ApiServer.Utils;
using Solace.Db.Earth;
using Solace.Db.Earth.Models.Player;
using Rewards = Solace.ApiServer.Utils.Rewards;
using Microsoft.EntityFrameworkCore;
using Solace.ObjectStore.Client;

namespace Solace.ApiServer.Controllers;

[Authorize]
[ApiVersion("1.1")]
[Route("1/api/v{version:apiVersion}/player/tokens")]
internal sealed class TokensController : SolaceControllerBase
{
    private readonly EarthDbContext _earthDb;
    private readonly ObjectStoreClient _objectStore;
    private readonly StaticData.StaticDataProvider _staticData;

    public TokensController(EarthDbContext earthDb, ObjectStoreClient objectStore, StaticData.StaticDataProvider staticData)
    {
        _earthDb = earthDb;
        _objectStore = objectStore;
        _staticData = staticData;
    }

    internal sealed record TokensResponse(
        Dictionary<Guid, Token> Tokens
    );

    internal sealed record RedeemedPlayerTokenResponse(
        Token Token,
        IReadOnlyDictionary<string, int?> Updates
    );

    [HttpGet]
    public async Task<Results<Ok<EarthApiResponse<TokensResponse>>, BadRequest>> Get(CancellationToken cancellationToken)
    {
        if (!TryGetProfileId(out var accountId))
        {
            return TypedResults.BadRequest();
        }

        var tokens = _earthDb.Tokens
            .AsNoTracking()
            .Where(token => token.ProfileId == accountId)
            .AsAsyncEnumerable();

        var tokensDictionary = await tokens
            .Select(token => new KeyValuePair<Guid, Token>(token.TokenId, TokenToApiResponse(token)))
            .ToDictionaryAsync(cancellationToken: cancellationToken);

        return TypedResults.Ok(new EarthApiResponse<TokensResponse>(new(
            tokensDictionary
        )));
    }

    [HttpPost("{tokenId}/redeem")]
    public async Task<Results<Ok<EarthApiResponse<RedeemedPlayerTokenResponse>>, BadRequest>> Redeem(Guid tokenId, CancellationToken cancellationToken)
    {
        if (!TryGetProfileId(out var accountId))
        {
            return TypedResults.BadRequest();
        }

        var requestStartedOn = HttpContext.GetTimestamp();

        var results = new ResultsEF.Builder();
        var removedToken = await TokenUtils.RedeemTokenAsync(_earthDb, results, _objectStore, accountId, tokenId, requestStartedOn, _staticData, cancellationToken);

        if (removedToken is not null)
        {
            var buildResults = await results.BuildAsync(_earthDb, accountId, cancellationToken);
            var updates = new EarthUpdatesResponse(buildResults);
            return TypedResults.Ok(new EarthApiResponse<RedeemedPlayerTokenResponse>(new(TokenToApiResponse(removedToken), updates.Map), updates));
        }
        else
        {
            return TypedResults.BadRequest();
        }
    }

    private static Token TokenToApiResponse(TokenEF token)
    {
        Dictionary<string, object> properties = [with(StringComparer.Ordinal)];
        switch (token)
        {
            case JournalItemUnlockedTokenEF journalItemUnlocked:
                properties["itemid"] = journalItemUnlocked.ItemId.ToString();
                break;
            case FTUESeasonsTokenEF ftueSeasons:
                properties["seasonId"] = ftueSeasons.SeasonId.ToString();
                break;
            case NewSeasonNotificationTokenEF newSeason:
                properties["seasonId"] = newSeason.SeasonId.ToString();
                break;
            case ChallengeProgressTokenEF challengeProgress:
                properties["challengeid"] = challengeProgress.ChallengeId.ToString();
                properties["challengereferenceid"] = challengeProgress.ChallengeId.ToString();
                properties["category"] = challengeProgress.Category.ToString().ToLowerInvariant();
                properties["currentcount"] = challengeProgress.CurrentCount;
                properties["previouscount"] = challengeProgress.PreviousCount;
                properties["totalthreshold"] = challengeProgress.TotalThreshold;
                var percent = challengeProgress.TotalThreshold > 0
                    ? int.Clamp(challengeProgress.CurrentCount * 100 / challengeProgress.TotalThreshold, 0, 100)
                    : 100;
                properties["percentcomplete"] = percent;
                break;
            case ChallengeCompleteTokenEF challengeComplete:
                properties["challengeid"] = challengeComplete.ChallengeId.ToString();
                if (challengeComplete.ExpirationTime.HasValue)
                {
                    properties["expirationtime"] = challengeComplete.ExpirationTime.Value.ToString("O");
                }

                break;
        }

        var rewards = token switch
        {
            LevelUpTokenEF levelUp => Rewards.FromDBRewardsModel(levelUp.Rewards).SetLevel(levelUp.Level),
            _ => new Rewards(),
        };

        var lifetime = token switch
        {
            LevelUpTokenEF => TokenLifetime.TRANSIENT,
            JournalItemUnlockedTokenEF => TokenLifetime.PERSISTENT,
            FTUESeasonsTokenEF => TokenLifetime.PERSISTENT,
            NewSeasonNotificationTokenEF => TokenLifetime.TRANSIENT,
            OOBESeasonChallengeSelectedTokenEF => TokenLifetime.TRANSIENT,
            ChallengeProgressTokenEF => TokenLifetime.TRANSIENT,
            ChallengeCompleteTokenEF => TokenLifetime.PERSISTENT,
            _ => throw new InvalidDataException($"Unknown Token type '{token?.GetType()?.ToString() ?? null}'"),
        };

        return new Token(
            TokenType.FromDb(token),
            properties,
            rewards.ToApiResponse(),
            lifetime
        );
    }
}
