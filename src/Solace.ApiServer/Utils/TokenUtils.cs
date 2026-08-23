using Microsoft.EntityFrameworkCore;
using Solace.Db.Earth;
using Solace.Db.Earth.Models.Player;
using Solace.ObjectStore.Client;
using Solace.StaticData;

namespace Solace.ApiServer.Utils;

internal static class TokenUtils
{
    public static async Task<TokenEF?> RedeemTokenAsync(EarthDbContext earthDb, ResultsEF.Builder results, ObjectStoreClient objectStore, Guid accountId, Guid tokenId, DateTimeOffset currentTime, StaticDataProvider staticData, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var transaction = await earthDb.Database.BeginTransactionAsync(cancellationToken);
            var token = await earthDb.Tokens
                .AsTracking()
                .FirstOrDefaultAsync(token => token.ProfileId == accountId && token.TokenId == tokenId, cancellationToken: cancellationToken);

            if (token is null or DailyLoginTokenEF { Claimed: true } ||
                token is DailyLoginTokenEF dailyLogin && dailyLogin.Date != DateOnly.FromDateTime(currentTime.UtcDateTime))
            {
                return null;
            }

            earthDb.Tokens.Remove(token);
            await earthDb.SaveChangesAsync(cancellationToken);
            results.Tokens();

            await DoActionsOnRedeemedTokenAsync(earthDb, results, objectStore, token, accountId, currentTime, staticData);
            await transaction.CommitAsync(cancellationToken);
            return token;
        }
        catch (DbUpdateConcurrencyException)
        {
            return null;
        }
    }

    public static async Task<Guid> AddTokenAsync(EarthDbContext earthDb, ResultsEF.Builder results, TokenEF token, CancellationToken cancellationToken = default)
    {
        earthDb.Tokens.Add(token);
        await earthDb.SaveChangesAsync(cancellationToken);

        results.Tokens();

        return token.TokenId;
    }

    public static async Task<TokenEF?> RemoveTokenAsync(EarthDbContext earthDb, ResultsEF.Builder results, Guid accountId, Guid tokenId, CancellationToken cancellationToken = default)
    {
        var token = await earthDb.Tokens
            .AsTracking()
            .FirstOrDefaultAsync(t => t.ProfileId == accountId && t.TokenId == tokenId, cancellationToken);

        if (token is null)
        {
            return null;
        }

        earthDb.Tokens.Remove(token);
        await earthDb.SaveChangesAsync(cancellationToken);

        results.Tokens();

        return token;
    }

    // does not handle redeeming the token itself (removing it from the list of tokens belonging to the player)
    public static async Task<TokenEF> DoActionsOnRedeemedTokenAsync(EarthDbContext earthDb, ResultsEF.Builder results, ObjectStoreClient objectStore, TokenEF token, Guid accountId, DateTimeOffset currentTime, StaticData.StaticDataProvider staticData)
    {
        switch (token)
        {
            case LevelUpTokenEF levelUpToken:
                {
                    await ActivityLogUtils.AddEntryAsync(earthDb, results, accountId, new LevelUpEntryEF(accountId, currentTime, levelUpToken.Level));

                    await Rewards.FromDBRewardsModel(levelUpToken.Rewards).ToRedeemQueryAsync(earthDb, results, objectStore, accountId, currentTime, staticData);
                }

                break;
            case JournalItemUnlockedTokenEF journalItemUnlockedToken:
                {
                    await ActivityLogUtils.AddEntryAsync(earthDb, results, accountId, new JournalItemUnlockedEntryEF(accountId, currentTime, journalItemUnlockedToken.ItemId));

                    /*int experiencePoints = staticData.catalog.itemsCatalog.getItem(journalItemUnlockedToken.itemId).experience().journal();
                    if (experiencePoints > 0)
                    {
                        updateQuery.then(new Rewards().addExperiencePoints(experiencePoints).toRedeemQuery(playerId, currentTime, staticData));
                    }*/
                }

                break;
            case DailyLoginTokenEF { Claimed: false, } dailyLoginToken:
                {
                    await Rewards.FromDBRewardsModel(dailyLoginToken.Rewards).ToRedeemQueryAsync(earthDb, results, objectStore, accountId, currentTime, staticData);
                }

                break;
        }

        return token;
    }
}
