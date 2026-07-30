using Microsoft.EntityFrameworkCore;
using Solace.Db.Earth;
using Solace.Db.Earth.Models.Player;

namespace Solace.ApiServer.Utils;

internal static class TokenUtils
{
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
    public static async Task<TokenEF> DoActionsOnRedeemedTokenAsync(EarthDbContext earthDb, ResultsEF.Builder results, TokenEF token, Guid accountId, DateTimeOffset currentTime, StaticData.StaticDataProvider staticData)
    {
        switch (token)
        {
            case LevelUpTokenEF levelUpToken:
                {
                    await ActivityLogUtils.AddEntryAsync(earthDb, results, accountId, new LevelUpEntryEF(accountId, currentTime, levelUpToken.Level));

                    await Rewards.FromDBRewardsModel(levelUpToken.Rewards).ToRedeemQueryAsync(earthDb, results, accountId, currentTime, staticData);
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
                    await Rewards.FromDBRewardsModel(dailyLoginToken.Rewards).ToRedeemQueryAsync(earthDb, results, accountId, currentTime, staticData);
                }

                break;
        }

        return token;
    }
}
