using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Solace.ApiServer.Exceptions;
using Solace.ApiServer.Types.Common;
using Solace.ApiServer.Utils;
using Solace.Common.Utils;
using Solace.DB;
using Solace.DB.Models.Player;
using Rewards = Solace.ApiServer.Utils.Rewards;
using Microsoft.EntityFrameworkCore;
using Solace.DB.Utils;
using System.Diagnostics;

namespace Solace.ApiServer.Controllers.EarthApi;

[Authorize]
[ApiVersion("1.1")]
[Route("1/api/v{version:apiVersion}/player/tokens")]
internal sealed class TokensController : SolaceControllerBase
{
    private readonly EarthDbContext _earthDb;
    private readonly StaticData.StaticData _staticData;

    public TokensController(EarthDbContext earthDb, StaticData.StaticData staticData)
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

        var tokens = await _earthDb.Tokens
            .AsNoTracking()
            .FirstOrNewAsync(tokens => tokens.Id == accountId, trackNew: false, cancellationToken: cancellationToken);

        return EarthJson(new Dictionary<string, Dictionary<string, Token>>()
        {
            {
                "tokens",
                tokens.GetTokens()
                    .Where(token => token.Token is not TokensEF.DailyLoginToken { Claimed: true })
                    .Select(token => new KeyValuePair<string, Token>(token.Id, TokenToApiResponse(token.Token)))
                    .ToDictionary()
            }
        }, null);
    }

    [HttpPost("{tokenId}/redeem")]
    public async Task<Results<ContentHttpResult, BadRequest>> Redeem(string tokenId, CancellationToken cancellationToken)
    {
        if (!TryGetAccountId(out var accountId))
        {
            return TypedResults.BadRequest();
        }

        // request.timestamp
        long requestStartedOn = HttpContext.GetTimestamp();

        var tokens = await _earthDb.Tokens
            .AsTracking()
            .FirstOrNewAsync(tokens => tokens.Id == accountId, trackNew: false, cancellationToken: cancellationToken);

        var removedToken = tokens.RemoveToken(tokenId);

        if (removedToken is not null)
        {
            await _earthDb.SaveChangesAsync(cancellationToken);

            await TokenUtils.DoActionsOnRedeemedTokenAsync(new EarthDbContext.Results(_earthDb), removedToken, accountId, requestStartedOn, _staticData);
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

    private static Token TokenToApiResponse(TokensEF.Token token)
    {
        Dictionary<string, string> properties = [];
        switch (token)
        {
            case TokensEF.JournalItemUnlockedToken journalItemUnlocked:
                properties["itemid"] = journalItemUnlocked.ItemId;
                break;
        }

        Rewards rewards = token switch
        {
            TokensEF.LevelUpToken levelUp => Rewards.FromDBRewardsModel(levelUp.Rewards).SetLevel(levelUp.Level),
            TokensEF.DailyLoginToken dailyLogin => Rewards.FromDBRewardsModel(dailyLogin.Rewards),
            _ => new Rewards(),
        };

        Token.LifetimeE lifetime = token switch
        {
            TokensEF.LevelUpToken => Token.LifetimeE.TRANSIENT,
            TokensEF.JournalItemUnlockedToken => Token.LifetimeE.PERSISTENT,
            TokensEF.DailyLoginToken => Token.LifetimeE.TRANSIENT,
            _ => throw new InvalidDataException($"Unknown Token type '{token?.GetType()?.ToString() ?? null}'"),
        };

        return new Token(
            Token.Type.FromDb(token.Type),
            properties,
            rewards.ToApiResponse(),
            lifetime
        );
    }
}
