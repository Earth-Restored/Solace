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

namespace Solace.ApiServer.Controllers;

[Authorize]
[ApiVersion("1.1")]
[Route("1/api/v{version:apiVersion}/player/tokens")]
internal sealed class TokensController : SolaceControllerBase
{
    private readonly EarthDbContext _earthDb;
    private readonly StaticData.StaticDataProvider _staticData;

    public TokensController(EarthDbContext earthDb, StaticData.StaticDataProvider staticData)
    {
        _earthDb = earthDb;
        _staticData = staticData;
    }

    [HttpGet]
    public async Task<Results<ContentHttpResult, BadRequest>> Get(CancellationToken cancellationToken)
    {
        if (!TryGetAccountId(out var accountId))
        {
            return TypedResults.BadRequest();
        }

        var tokens = _earthDb.Tokens
            .AsNoTracking()
            .Where(token => token.ProfileId == accountId)
            .AsAsyncEnumerable();

        var tokensResponse = await tokens.Where(token => token is not DailyLoginTokenEF { Claimed: true, })
            .Select(token => new KeyValuePair<Guid, Token>(token.TokenId, TokenToApiResponse(token)))
            .ToDictionaryAsync(cancellationToken: cancellationToken);

        return EarthJson(new Dictionary<string, Dictionary<Guid, Token>>(StringComparer.Ordinal)
        {
            {
                "tokens",
                tokensResponse
            }
        }, null);
    }

    [HttpPost("{tokenId}/redeem")]
    public async Task<Results<ContentHttpResult, BadRequest>> Redeem(Guid tokenId, CancellationToken cancellationToken)
    {
        if (!TryGetAccountId(out var accountId))
        {
            return TypedResults.BadRequest();
        }

        // request.timestamp
        var requestStartedOn = HttpContext.GetTimestamp();

        var removedToken = await TokenUtils.RemoveTokenAsync(_earthDb, ResultsEF.Builder.Null, accountId, tokenId, cancellationToken);

        if (removedToken is not null)
        {
            await TokenUtils.DoActionsOnRedeemedTokenAsync(_earthDb, ResultsEF.Builder.Null, removedToken, accountId, requestStartedOn, _staticData);
        }

        if (removedToken is not null)
        {
            return EarthJson(TokenToApiResponse(removedToken));
        }
        else
        {
            return TypedResults.BadRequest();
        }
    }

    private static Token TokenToApiResponse(TokenEF token)
    {
        Dictionary<string, string> properties = [];
        switch (token)
        {
            case JournalItemUnlockedTokenEF journalItemUnlocked:
                properties["itemid"] = journalItemUnlocked.ItemId.ToString();
                break;
        }

        Rewards rewards = token switch
        {
            LevelUpTokenEF levelUp => Rewards.FromDBRewardsModel(levelUp.Rewards).SetLevel(levelUp.Level),
            DailyLoginTokenEF dailyLogin => Rewards.FromDBRewardsModel(dailyLogin.Rewards),
            _ => new Rewards(),
        };

        Token.LifetimeE lifetime = token switch
        {
            LevelUpTokenEF => Token.LifetimeE.TRANSIENT,
            JournalItemUnlockedTokenEF => Token.LifetimeE.PERSISTENT,
            DailyLoginTokenEF => Token.LifetimeE.TRANSIENT,
            _ => throw new InvalidDataException($"Unknown Token type '{token?.GetType()?.ToString() ?? null}'"),
        };

        return new Token(
            Token.Type.FromDb(token),
            properties,
            rewards.ToApiResponse(),
            lifetime
        );
    }
}
