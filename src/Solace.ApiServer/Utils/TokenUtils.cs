using Serilog;
using Solace.Common.Utils;
using Solace.ApiServer.Controllers.EarthApi;
using Solace.DB;
using Solace.DB.Models.Player;

namespace Solace.ApiServer.Utils;

public static class TokenUtils
{
    private const string CommonAdventureCrystalId = "4f16a053-4929-263a-c91a-29663e29df76";

    public static EarthDB.Query AddToken(string playerId, Tokens.Token token)
    {
        var getQuery = new EarthDB.Query(true);
        getQuery.Get("tokens", playerId, typeof(Tokens));
        getQuery.Then(results =>
        {
            Tokens tokens = results.Get<Tokens>("tokens");
            string id = U.RandomUuid().ToString();
            tokens.AddToken(id, token);
            var updateQuery = new EarthDB.Query(true);
            updateQuery.Update("tokens", playerId, tokens);
            updateQuery.Extra("tokenId", id);
            return updateQuery;
        });
        return getQuery;
    }

    public static async Task EnsureDailyLoginToken(string playerId, CancellationToken cancellationToken)
    {
        string today = DateTimeOffset.UtcNow.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);

        try
        {
            await new EarthDB.Query(true)
                .Get("tokenClaims", playerId, typeof(TokenClaims))
                .Get("tokens", playerId, typeof(Tokens))
                .Then(results =>
                {
                    TokenClaims tokenClaims = results.Get<TokenClaims>("tokenClaims");
                    Tokens tokens = results.Get<Tokens>("tokens");

                    bool changed = false;
                    foreach (Tokens.TokenWithId existingToken in tokens.GetTokens())
                    {
                        if (existingToken.Token is Tokens.DailyLoginToken dailyLoginToken && dailyLoginToken.Date != today)
                        {
                            tokens.RemoveToken(existingToken.Id);
                            changed = true;
                        }

                        if (existingToken.Token is Tokens.DailyLoginToken dailyLoginTokenToday && dailyLoginTokenToday.Date == today && !Guid.TryParse(existingToken.Id, out _))
                        {
                            tokens.RemoveToken(existingToken.Id);
                            changed = true;
                        }
                    }

                    if (tokenClaims.RedeemedDailyLoginDates.Contains(today))
                    {
                        foreach (Tokens.TokenWithId existingToken in tokens.GetTokens())
                        {
                            if (existingToken.Token is Tokens.DailyLoginToken dailyLoginToken && dailyLoginToken.Date == today)
                            {
                                tokens.RemoveToken(existingToken.Id);
                                changed = true;
                            }
                        }

                        return changed
                            ? new EarthDB.Query(true).Update("tokens", playerId, tokens)
                            : EarthDB.Query.Empty;
                    }

                    if (tokens.GetTokens().Any(token => token.Token is Tokens.DailyLoginToken dailyLoginToken && dailyLoginToken.Date == today && Guid.TryParse(token.Id, out _)))
                    {
                        return changed
                            ? new EarthDB.Query(true).Update("tokens", playerId, tokens)
                            : EarthDB.Query.Empty;
                    }

                    tokenClaims.DailyLoginStreak = tokenClaims.LastDailyLoginDate is null || tokenClaims.LastDailyLoginDate == today
                        ? Math.Max(1, tokenClaims.DailyLoginStreak)
                        : tokenClaims.DailyLoginStreak + 1;
                    tokenClaims.LastDailyLoginDate = today;

                    tokens.AddToken(U.RandomUuid().ToString(), new Tokens.DailyLoginToken(
                        today,
                        new Solace.DB.Models.Common.Rewards(
                            0,
                            25,
                            null,
                            new Dictionary<string, int?> { [CommonAdventureCrystalId] = 1 },
                            [],
                            [])));

                    return new EarthDB.Query(true)
                        .Update("tokenClaims", playerId, tokenClaims)
                        .Update("tokens", playerId, tokens);
                })
                .ExecuteAsync(Program.DB, cancellationToken);
        }
        catch (EarthDB.DatabaseException exception)
        {
            Log.Warning(exception, "Could not create daily login token for {PlayerId}", playerId);
        }
    }

    // does not handle redeeming the token itself (removing it from the list of tokens belonging to the player)
    public static EarthDB.Query DoActionsOnRedeemedToken(Tokens.Token token, string playerId, long currentTime, StaticData.StaticData staticData)
    {
        var getQuery = new EarthDB.Query(true);

        switch (token.Type)
        {
            case Tokens.Token.TypeE.LEVEL_UP:
                {
                    var levelUpToken = (Tokens.LevelUpToken)token;

                    getQuery.Then(results =>
                    {
                        var updateQuery = new EarthDB.Query(true);

                        updateQuery.Then(ActivityLogUtils.AddEntry(playerId, new ActivityLog.LevelUpEntry(currentTime, levelUpToken.Level)));

                        updateQuery.Then(Rewards.FromDBRewardsModel(levelUpToken.Rewards).ToRedeemQuery(playerId, currentTime, staticData));

                        return updateQuery;
                    }, false);
                }

                break;
            case Tokens.Token.TypeE.JOURNAL_ITEM_UNLOCKED:
                {
                    var journalItemUnlockedToken = (Tokens.JournalItemUnlockedToken)token;
                    getQuery.Then(results =>
                    {
                        var updateQuery = new EarthDB.Query(true);

                        updateQuery.Then(ActivityLogUtils.AddEntry(playerId, new ActivityLog.JournalItemUnlockedEntry(currentTime, journalItemUnlockedToken.ItemId)));

                        /*int experiencePoints = staticData.catalog.itemsCatalog.getItem(journalItemUnlockedToken.itemId).experience().journal();
                        if (experiencePoints > 0)
                        {
                            updateQuery.then(new Rewards().addExperiencePoints(experiencePoints).toRedeemQuery(playerId, currentTime, staticData));
                        }*/

                        return updateQuery;
                    }, false);
                }

                break;
            case Tokens.Token.TypeE.DAILY_LOGIN:
                {
                    var dailyLoginToken = (Tokens.DailyLoginToken)token;
                    getQuery.Get("tokenClaims", playerId, typeof(TokenClaims));
                    getQuery.Then(results =>
                    {
                        TokenClaims tokenClaims = results.Get<TokenClaims>("tokenClaims");
                        tokenClaims.LastDailyLoginDate = dailyLoginToken.Date;
                        tokenClaims.RedeemedDailyLoginDates.Add(dailyLoginToken.Date);

                        return new EarthDB.Query(true)
                            .Update("tokenClaims", playerId, tokenClaims)
                            .Then(Rewards.FromDBRewardsModel(dailyLoginToken.Rewards).ToRedeemQuery(playerId, currentTime, staticData), false);
                    }, false);
                }

                break;
            case Tokens.Token.TypeE.OOBE_ADVENTURE_CRYSTAL:
                {
                    var oobeAdventureCrystalToken = (Tokens.OobeAdventureCrystalToken)token;
                    getQuery.Then(Rewards.FromDBRewardsModel(oobeAdventureCrystalToken.Rewards).ToRedeemQuery(playerId, currentTime, staticData), false);
                }

                break;
            case Tokens.Token.TypeE.CHALLENGE_PROGRESS:
                break;
            case Tokens.Token.TypeE.CHALLENGE_COMPLETED:
                {
                    var completedToken = (Tokens.ChallengeCompletedToken)token;
                    getQuery.Get("challenges", playerId, typeof(ChallengeProgressVersion));
                    getQuery.Then(results =>
                    {
                        ChallengeProgressVersion progress = results.Get<ChallengeProgressVersion>("challenges");
                        progress.EnsureDate(currentTime);
                        progress.ClaimedChallengeIds ??= [];
                        bool firstClaim = progress.ClaimedChallengeIds.Add(completedToken.ChallengeId);
                        progress.ActiveSeasonId = ChallengesController.ActiveSeasonId;
                        progress.ActiveSeasonChallengeId = ChallengesController.SelectActiveSeasonChallengeId(progress, progress.ActiveSeasonChallengeId);
                        progress.UpdatedAt = currentTime;

                        var updateQuery = new EarthDB.Query(true)
                            .Update("challenges", playerId, progress);

                        if (firstClaim)
                        {
                            Types.Common.Rewards? apiRewards = ChallengesController.GetSeasonChallengeRewards(completedToken.ChallengeId);
                            if (apiRewards is not null)
                            {
                                updateQuery.Then(ToRedeemRewards(apiRewards).ToRedeemQuery(playerId, currentTime, staticData), false);
                            }
                        }

                        return updateQuery;
                    }, false);
                }

                break;
        }

        getQuery.Extra("token", token);

        return getQuery;
    }

    private static Rewards ToRedeemRewards(Types.Common.Rewards rewards)
    {
        var result = new Rewards();
        if (rewards.Rubies is > 0)
        {
            result.AddRubies(rewards.Rubies.Value);
        }

        if (rewards.ExperiencePoints is > 0)
        {
            result.AddExperiencePoints(rewards.ExperiencePoints.Value);
        }

        foreach (var item in rewards.Inventory)
        {
            result.AddItem(item.Id, item.Amount);
        }

        foreach (string buildplateId in rewards.Buildplates)
        {
            result.AddBuildplate(buildplateId);
        }

        foreach (var challenge in rewards.Challenges)
        {
            result.AddChallenge(challenge.Id);
        }

        return result;
    }
}
