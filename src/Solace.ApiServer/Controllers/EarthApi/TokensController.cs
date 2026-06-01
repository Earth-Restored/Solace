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

namespace Solace.ApiServer.Controllers.EarthApi;

[Authorize]
[ApiVersion("1.1")]
[Route("1/api/v{version:apiVersion}/player/tokens")]
internal sealed class TokensController : SolaceControllerBase
{
    private const string DailyGroupId = "29ebe650-072f-4f70-996f-4ffdda93ed1f";
    private const string DailyReferenceId = "2619913d-6504-4c74-9fc9-e03649a70efc";
    private static EarthDB earthDB => Program.DB;
    private static StaticData.StaticData staticData => Program.staticData;

    [HttpGet]
    public async Task<Results<ContentHttpResult, BadRequest>> Get(CancellationToken cancellationToken)
    {
        DisableClientCache();

        string? playerId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(playerId))
        {
            return TypedResults.BadRequest();
        }

        await TokenUtils.EnsureDailyLoginToken(playerId, cancellationToken);

        Tokens tokens = (await new EarthDB.Query(false)
            .Get("tokens", playerId, typeof(Tokens))
            .ExecuteAsync(earthDB, cancellationToken))
            .Get<Tokens>("tokens");

        return EarthJson(new Dictionary<string, Dictionary<string, Token>>()
        {
            {
                "tokens",
                tokens.GetTokens().Select(token => new KeyValuePair<string, Token>(token.Id, TokenToApiResponse(token.Token))).ToDictionary()
            }
        }, null);
    }

    [HttpPost("{tokenId}/redeem")]
    public async Task<Results<ContentHttpResult, BadRequest>> Redeem(string tokenId, CancellationToken cancellationToken)
    {
        DisableClientCache();

        string? playerId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(playerId))
        {
            return TypedResults.BadRequest();
        }

        // request.timestamp
        long requestStartedOn = HttpContext.GetTimestamp();

        Tokens.Token? token;
        EarthDB.Results results;
        try
        {
            results = await new EarthDB.Query(true)
                .Get("tokens", playerId, typeof(Tokens))
                .Then(results1 =>
                {
                    Tokens tokens = results1.Get<Tokens>("tokens");
                    Tokens.Token? removedToken = tokens.RemoveToken(tokenId);
                    if (removedToken is not null)
                    {
                        return new EarthDB.Query(true)
                            .Update("tokens", playerId, tokens)
                            .Then(TokenUtils.DoActionsOnRedeemedToken(removedToken, playerId, requestStartedOn, staticData), false)
                            .Extra("success", true)
                            .Extra("token", removedToken);
                    }
                    else
                    {
                        return new EarthDB.Query(false)
                            .Extra("success", false);
                    }
                })
                .ExecuteAsync(earthDB, cancellationToken);
            token = (bool)results.GetExtra("success") ? (Tokens.Token)results.GetExtra("token") : null;
        }
        catch (EarthDB.DatabaseException ex)
        {
            throw new ServerErrorException(ex);
        }

        if (token is not null)
        {
            var updates = new EarthApiResponse.UpdatesResponse(results);
            if (token is Tokens.ChallengeProgressToken or Tokens.ChallengeCompletedToken or Tokens.DailyLoginToken)
            {
                updates.Map["challenges"] = (int)(requestStartedOn / 1000);
            }

            return EarthJson(TokenToApiResponse(token), updates);
        }
        else
        {
            return TypedResults.BadRequest();
        }
    }

    internal static Token TokenToApiResponse(Tokens.Token token)
    {
        Dictionary<string, string> properties = [];
        switch (token)
        {
            case Tokens.JournalItemUnlockedToken journalItemUnlocked:
                properties["itemid"] = journalItemUnlocked.ItemId;
                break;
            case Tokens.DailyLoginToken dailyLogin:
                properties["date"] = dailyLogin.Date;
                properties["login_count"] = "7";
                properties["challengeid"] = DailyGroupId;
                properties["challengereferenceid"] = DailyReferenceId;
                break;
            case Tokens.OobeAdventureCrystalToken oobeAdventureCrystal:
                properties["itemid"] = oobeAdventureCrystal.ItemId;
                break;
            case Tokens.ChallengeProgressToken challengeProgress:
                properties["challengeid"] = challengeProgress.ChallengeId;
                properties["challengereferenceid"] = challengeProgress.ChallengeReferenceId;
                break;
            case Tokens.ChallengeCompletedToken challengeCompleted:
                properties["challengeid"] = challengeCompleted.ChallengeId;
                properties["challengereferenceid"] = challengeCompleted.ChallengeReferenceId;
                break;
        }

        Rewards rewards = token switch
        {
            Tokens.LevelUpToken levelUp => Rewards.FromDBRewardsModel(levelUp.Rewards).SetLevel(((Tokens.LevelUpToken)token).Level),
            Tokens.DailyLoginToken dailyLogin => Rewards.FromDBRewardsModel(dailyLogin.Rewards),
            Tokens.OobeAdventureCrystalToken oobeAdventureCrystal => Rewards.FromDBRewardsModel(oobeAdventureCrystal.Rewards),
            _ => new Rewards(),
        };

        Token.LifetimeE lifetime = token switch
        {
            Tokens.LevelUpToken => Token.LifetimeE.TRANSIENT,
            Tokens.JournalItemUnlockedToken => Token.LifetimeE.PERSISTENT,
            Tokens.DailyLoginToken => Token.LifetimeE.TRANSIENT,
            Tokens.OobeAdventureCrystalToken => Token.LifetimeE.PERSISTENT,
            Tokens.ChallengeProgressToken => Token.LifetimeE.TRANSIENT,
            Tokens.ChallengeCompletedToken => Token.LifetimeE.TRANSIENT,
            _ => throw new InvalidDataException($"Unknown Token type '{token?.GetType()?.ToString() ?? null}'"),
        };

        Token.Type clientType = token switch
        {
            // 0.33's native client has no `daily.login` clientType parser. Reuse the
            // sign-in challenge notification path so the token is visible and redeemable.
            Tokens.DailyLoginToken => Token.Type.CHALLENGE_COMPLETED,
            _ => Enum.Parse<Token.Type>(token.Type.ToString()),
        };

        return new Token(
                clientType,
                properties,
                rewards.ToApiResponse(),
                lifetime
        );
    }

    private void DisableClientCache()
    {
        Response.Headers.CacheControl = "no-store, no-cache, max-age=0";
        Response.Headers.Pragma = "no-cache";
        Response.Headers.Expires = "0";
        Response.Headers.ETag = $"\"tokens-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}\"";
    }
}
