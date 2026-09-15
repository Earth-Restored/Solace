using System.Diagnostics;

namespace Solace.ApiServer.Types.Common;

internal static class TokenTypeExtensions
{
    extension(TokenType)
    {
        public static TokenType FromDb(Db.Earth.Models.Player.TokenEF token)
            => token switch
            {
                Db.Earth.Models.Player.LevelUpTokenEF => TokenType.LEVEL_UP,
                Db.Earth.Models.Player.JournalItemUnlockedTokenEF => TokenType.JOURNAL_ITEM_UNLOCKED,
                Db.Earth.Models.Player.DailyLoginTokenEF => TokenType.DAILY_LOGIN,
                _ => throw new UnreachableException(),
            };
    }
}
