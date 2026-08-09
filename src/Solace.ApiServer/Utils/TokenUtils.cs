using Microsoft.EntityFrameworkCore;
using Solace.Common.Utils;
using Solace.ApiServer.Controllers.EarthApi;
using Solace.DB;
using Solace.DB.Models.Player;
using Solace.DB.Utils;
using System.Globalization;

namespace Solace.ApiServer.Utils;

public static class TokenUtils
{
    public static async Task<TokensEF.Token?> RedeemTokenAsync(
        EarthDbContext.Results results,
        Guid accountId,
        string tokenId,
        long currentTime,
        StaticData.StaticData staticData,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var transaction = await results.EarthDb.Database.BeginTransactionAsync(cancellationToken);
            var tokens = await results.EarthDb.Tokens
                .AsTracking()
                .FirstOrNewAsync(tokens => tokens.Id == accountId, trackNew: false, cancellationToken: cancellationToken);
            if (!tokens.Tokens.TryGetValue(tokenId, out TokensEF.Token? token) ||
                token is TokensEF.ChallengeProgressToken or TokensEF.DailyLoginToken { Claimed: true } ||
                token is TokensEF.DailyLoginToken dailyLogin && dailyLogin.Date != UtcDate(currentTime))
            {
                return null;
            }

            TokensEF.Token removedToken = tokens.RemoveToken(tokenId)!;

            await results.EarthDb.SaveChangesAsync(cancellationToken);
            results.Tokens = tokens.Version;
            await DoActionsOnRedeemedTokenAsync(results, removedToken, accountId, currentTime, staticData);
            await transaction.CommitAsync(cancellationToken);
            return removedToken;
        }
        catch (DbUpdateConcurrencyException)
        {
            return null;
        }
    }

    public static async Task<string> AddTokenAsync(EarthDbContext.Results results, Guid accountId, TokensEF.Token token)
    {
        var tokens = await results.EarthDb.Tokens
            .AsTracking()
            .FirstOrNewAsync(tokens => tokens.Id == accountId);

        string id = Guid.NewGuid().ToString();
        tokens.AddToken(id, token);

        await results.EarthDb.SaveChangesAsync();

        results.Tokens = tokens.Version;

        return id;
    }

    // does not handle redeeming the token itself (removing it from the list of tokens belonging to the player)
    public static async Task<TokensEF.Token> DoActionsOnRedeemedTokenAsync(EarthDbContext.Results results, TokensEF.Token token, Guid accountId, long currentTime, StaticData.StaticData staticData)
    {
        switch (token)
        {
            case TokensEF.LevelUpToken levelUpToken:
                {
                    await ActivityLogUtils.AddEntryAsync(results, accountId, new ActivityLogEF.LevelUpEntry(currentTime, levelUpToken.Level));

                    await Rewards.FromDBRewardsModel(levelUpToken.Rewards).ToRedeemQueryAsync(results, accountId, currentTime, staticData);
                }

                break;
            case TokensEF.JournalItemUnlockedToken journalItemUnlockedToken:
                {
                    await ActivityLogUtils.AddEntryAsync(results, accountId, new ActivityLogEF.JournalItemUnlockedEntry(currentTime, journalItemUnlockedToken.ItemId));

                    /*int experiencePoints = staticData.catalog.itemsCatalog.getItem(journalItemUnlockedToken.itemId).experience().journal();
                    if (experiencePoints > 0)
                    {
                        updateQuery.then(new Rewards().addExperiencePoints(experiencePoints).toRedeemQuery(playerId, currentTime, staticData));
                    }*/
                }

                break;
            case TokensEF.DailyLoginToken { Claimed: false } dailyLoginToken:
                {
                    var tokens = await results.EarthDb.Tokens
                        .AsTracking()
                        .FirstOrNewAsync(tokens => tokens.Id == accountId);
                    TokensEF.ChallengeProgressToken stored = tokens.Tokens.TryGetValue(ChallengesController.ProgressTokenId, out TokensEF.Token? raw) &&
                        raw is TokensEF.ChallengeProgressToken progressToken
                        ? progressToken
                        : new TokensEF.ChallengeProgressToken();
                    var progress = ChallengeProgressVersion.FromToken(stored);
                    if (progress.IsDailyLoginClaimed(currentTime))
                    {
                        break;
                    }

                    progress.ClaimDailyLogin(currentTime);
                    tokens.AddToken(ChallengesController.ProgressTokenId, progress.ToToken());
                    await results.EarthDb.SaveChangesAsync();
                    results.Tokens = tokens.Version;
                    await Rewards.FromDBRewardsModel(dailyLoginToken.Rewards).ToRedeemQueryAsync(results, accountId, currentTime, staticData);
                }

                break;
        }

        return token;
    }

    private static string UtcDate(long timestamp)
        => DateTimeOffset.FromUnixTimeMilliseconds(timestamp).UtcDateTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}
