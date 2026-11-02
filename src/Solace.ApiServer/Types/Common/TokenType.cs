using System.Diagnostics;
using System.Text.Json.Serialization;

namespace Solace.ApiServer.Types.Common;

/*
* lookup_funcs.json at 0x445c2cc
* sub_1C5D658 (0x1c5d658) - array starting at 0x50ff264 (unk_50FF264) through 0x50ff30c
* 32-bit FNV-1a hashes
*/
[JsonConverter(typeof(JsonStringEnumConverter<TokenType>))]
internal enum TokenType
{
#pragma warning disable CA1707 // Identifiers should not contain underscores

    [JsonStringEnumMemberName("1fciz2")]
    EULA_VERSION,

    [JsonStringEnumMemberName("adv_zyki")]
    LEVEL_UP,

    [JsonStringEnumMemberName("vhlflc")]
    LEVEL_UP_REWARDS_MODAL,

    [JsonStringEnumMemberName("rqrs7xb")]
    MIGRATED_TO_PLAYFAB_ENTITLEMENTS,

    [JsonStringEnumMemberName("jko_8nb")]
    OOBE_PERMISSIONS,

    [JsonStringEnumMemberName("s1zb6")]
    OOBE_STARTER_KIT,

    [JsonStringEnumMemberName("50_998a")]
    OOBE_ADVENTURE_SPAWN_CRYSTAL,

    [JsonStringEnumMemberName("4070xub")]
    OOBE_ADVENTURE_IN_AR,

    [JsonStringEnumMemberName("nb6z3ia")]
    OOBE_FAKE_MAP,

    [JsonStringEnumMemberName("ebz2nw")]
    OOBE_REQUEST_PERMISSION_LOCATION,

    [JsonStringEnumMemberName("9g-wufa")]
    OOBE_FAKE_TAPPABLE,

    [JsonStringEnumMemberName("_.mrrq")]
    OOBE_CHALLENGES,

    [JsonStringEnumMemberName("d455j.a")]
    OOBE_CHALLENGES_VISIBLE,

    [JsonStringEnumMemberName(".afju7a")]
    OOBE_JOURNAL,

    [JsonStringEnumMemberName("ll5adl")]
    OOBE_FREEDOM,

    [JsonStringEnumMemberName("z3w7np")]
    OOBE_SEASON_CHALLENGE_SELECTED,

    [JsonStringEnumMemberName("10rzjf")]
    FTUE_WELCOME,

    [JsonStringEnumMemberName("0094tla")]
    FTUE_SEASONS,

    [JsonStringEnumMemberName("8_h9i_a")]
    NEW_SEASON_NOTIFICATION,

    [JsonStringEnumMemberName("bt.a6fc")]
    FTUE_TAPPABLES,

    [JsonStringEnumMemberName("efllbwc")]
    FTUE_INVENTORY,

    [JsonStringEnumMemberName("1rh-msa")]
    FTUE_BUILDPLATE,

    [JsonStringEnumMemberName("x44qj1")]
    FTUE_ADVENTURES,

    [JsonStringEnumMemberName("10t0gqc")]
    FTUE_ADVENTURE_AR_PLACEMENT,

    [JsonStringEnumMemberName("1gywsxd")]
    FTUE_CRYSTAL_ACTIVATION,

    [JsonStringEnumMemberName("is-0bca")]
    FTUE_ADVENTURE_CRYSTAL_PREVIEW,

    [JsonStringEnumMemberName("17jpc_")]
    FIRST_TIME_VISITED_STORE,

    [JsonStringEnumMemberName("3o7ggi")]
    FTUE_CRAFTING_GIFT,

    [JsonStringEnumMemberName("zujlyqa")]
    FTUE_CRAFTING_CRAFT,

    [JsonStringEnumMemberName("v-vbt5")]
    FTUE_CRAFTING_COLLECT,

    [JsonStringEnumMemberName("zvm581c")]
    FTUE_SMELTING_GIFT,

    [JsonStringEnumMemberName("on0wyaa")]
    FTUE_SMELTING_SMELT,

    [JsonStringEnumMemberName("had_c-")]
    FTUE_SMELTING_COLLECT,

    [JsonStringEnumMemberName("9c7k99d")]
    SMELTING_FUEL_BUCKET_RETURN,

    [JsonStringEnumMemberName("challenge.progress")]
    CHALLENGE_PROGRESS,

    [JsonStringEnumMemberName("challenge.completed")]
    CHALLENGE_COMPLETE,

    [JsonStringEnumMemberName("wkshps")]
    HIDDEN_CHALLENGE_REWARD,

    [JsonStringEnumMemberName("i86z-xd")]
    ADVENTURE_REWARD,

    [JsonStringEnumMemberName("m6j-gn")]
    JOURNAL_ITEM_FOUND,

    [JsonStringEnumMemberName("item.unlocked")]
    JOURNAL_ITEM_UNLOCKED,

    [JsonStringEnumMemberName("jryn-b")]
    SUNSET_OVERLAY,

    [JsonStringEnumMemberName("am2l9s")]
    SUNSET_COMMEMORATIVE_SKINS,

    [JsonStringEnumMemberName("redeemtappable")]
    TAPPABLE,

    [JsonStringEnumMemberName("daily.login")] // unconfirmed
    DAILY_LOGIN,

#pragma warning restore CA1707 // Identifiers should not contain underscores
}

#pragma warning disable MA0048 // File name must match type name
internal static class TokenTypeExtensions
#pragma warning restore MA0048 // File name must match type name
{
    extension(TokenType)
    {
        public static TokenType FromDb(Db.Earth.Models.Player.TokenEF token)
            => token switch
            {
                Db.Earth.Models.Player.LevelUpTokenEF => TokenType.LEVEL_UP,
                Db.Earth.Models.Player.JournalItemUnlockedTokenEF => TokenType.JOURNAL_ITEM_UNLOCKED,
                Db.Earth.Models.Player.FTUESeasonsTokenEF => TokenType.FTUE_SEASONS,
                Db.Earth.Models.Player.NewSeasonNotificationTokenEF => TokenType.NEW_SEASON_NOTIFICATION,
                Db.Earth.Models.Player.OOBESeasonChallengeSelectedTokenEF => TokenType.OOBE_SEASON_CHALLENGE_SELECTED,
                Db.Earth.Models.Player.ChallengeProgressTokenEF => TokenType.CHALLENGE_PROGRESS,
                Db.Earth.Models.Player.ChallengeCompleteTokenEF => TokenType.CHALLENGE_COMPLETE,
                _ => throw new UnreachableException(),
            };
    }
}
