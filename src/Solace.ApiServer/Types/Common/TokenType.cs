using System.Text.Json.Serialization;

namespace Solace.ApiServer.Types.Common;

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

    [JsonStringEnumMemberName("bi.i1qd")]
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

    [JsonStringEnumMemberName("daily.login")]
    DAILY_LOGIN,

#pragma warning restore CA1707 // Identifiers should not contain underscores
}
