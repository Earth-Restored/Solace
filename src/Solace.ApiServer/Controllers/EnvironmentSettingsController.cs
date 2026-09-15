using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json.Serialization;
using Solace.ApiServer.Utils;

namespace Solace.ApiServer.Controllers;

[Authorize]
[ApiVersion("1.1")]
[Route("1/api/v{version:apiVersion}")]
[ApiController]
internal sealed class EnvironmentSettingsController : ControllerBase
{
    internal sealed record FeatureFlags
    {
        [JsonPropertyName("workshop_enabled")]
        public required bool WorkshopEnabled { get; init; }

        [JsonPropertyName("buildplates_enabled")]
        public required bool BuildplatesEnabled { get; init; }

        [JsonPropertyName("enable_ruby_purchasing")]
        public required bool EnableRubyPurchasing { get; init; }

        [JsonPropertyName("commerce_enabled")]
        public required bool CommerceEnabled { get; init; }

        [JsonPropertyName("full_logging_enabled")]
        public required bool FullLoggingEnabled { get; init; }

        [JsonPropertyName("challenges_enabled")]
        public required bool ChallengesEnabled { get; init; }

        [JsonPropertyName("craftingv2_enabled")]
        public required bool CraftingV2Enabled { get; init; }

        [JsonPropertyName("smeltingv2_enabled")]
        public required bool SmeltingV2Enabled { get; init; }

        [JsonPropertyName("inventory_item_boosts_enabled")]
        public required bool InventoryItemBoostsEnabled { get; init; }

        [JsonPropertyName("player_health_enabled")]
        public required bool PlayerHealthEnabled { get; init; }

        [JsonPropertyName("minifigs_enabled")]
        public required bool MinifigsEnabled { get; init; }

        // boosts
        [JsonPropertyName("potions_enabled")]
        public required bool PotionsEnabled { get; init; }

        [JsonPropertyName("social_link_launch_enabled")]
        public required bool SocialLinkLaunchEnabled { get; init; }

        [JsonPropertyName("social_link_share_enabled")]
        public required bool SocialLinkShareEnabled { get; init; }

        [JsonPropertyName("encoded_join_enabled")]
        public required bool EncodedJoinEnabled { get; init; }

        [JsonPropertyName("adventure_crystals_enabled")]
        public required bool AdventureCrystalsEnabled { get; init; }

        [JsonPropertyName("item_limits_enabled")]
        public required bool ItemLimitsEnabled { get; init; }

        [JsonPropertyName("adventure_crystals_ftue_enabled")]
        public required bool AdventureCrystalsFtueEnabled { get; init; }

        [JsonPropertyName("expire_crystals_on_cleanup_enabled")]
        public required bool ExpireCrystalsOnCleanupEnabled { get; init; }

        [JsonPropertyName("challenges_v2_enabled")]
        public required bool ChallengesV2Enabled { get; init; }

        [JsonPropertyName("player_journal_enabled")]
        public required bool PlayerJournalEnabled { get; init; }

        [JsonPropertyName("player_stats_enabled")]
        public required bool PlayerStatsEnabled { get; init; }

        [JsonPropertyName("activity_log_enabled")]
        public required bool ActivityLogEnabled { get; init; }

        [JsonPropertyName("seasons_enabled")]
        public required bool SeasonsEnabled { get; init; }

        [JsonPropertyName("daily_login_enabled")]
        public required bool DailyLoginEnabled { get; init; }

        [JsonPropertyName("store_pdp_enabled")]
        public required bool StorePdpEnabled { get; init; }

        [JsonPropertyName("hotbar_stacksplitting_enabled")]
        public required bool HotbarStacksplittingEnabled { get; init; }

        [JsonPropertyName("fancy_rewards_screen_enabled")]
        public required bool FancyRewardsScreenEnabled { get; init; }

        [JsonPropertyName("async_ecs_dispatcher")]
        public required bool AsyncEcsDispatcher { get; init; }

        [JsonPropertyName("adventure_oobe_enabled")]
        public required bool AdventureOobeEnabled { get; init; }

        [JsonPropertyName("tappable_oobe_enabled")]
        public required bool TappableOobeEnabled { get; init; }

        [JsonPropertyName("map_permission_oobe_enabled")]
        public required bool MapPermissionOobeEnabled { get; init; }

        [JsonPropertyName("journal_oobe_enabled")]
        public required bool JournalOobeEnabled { get; init; }

        [JsonPropertyName("freedom_oobe_enabled")]
        public required bool FreedomOobeEnabled { get; init; }

        [JsonPropertyName("challenge_oobe_enabled")]
        public required bool ChallengeOobeEnabled { get; init; }

        [JsonPropertyName("level_rewards_v2_enabled")]
        public required bool LevelRewardsV2Enabled { get; init; }

        [JsonPropertyName("content_driven_season_assets")]
        public required bool ContentDrivenSeasonAssets { get; init; }

        [JsonPropertyName("paid_earned_rubies_enabled")]
        public required bool PaidEarnedRubiesEnabled { get; init; }
    }

    internal sealed class SettingsResponse
    {
        [JsonPropertyName("encounterinteractionradius")]
        public required int EncounterInteractionRadius { get; init; }

        [JsonPropertyName("tappableinteractionradius")]
        public required int TappableInteractionRadius { get; init; }

        [JsonPropertyName("tappablevisibleradius")]
        public required int TappableVisibleRadius { get; init; }

        [JsonPropertyName("targetpossibletappables")]
        public required int TargetPossibleTappables { get; init; }

        [JsonPropertyName("tile0")]
        public required int Tile0 { get; init; }

        [JsonPropertyName("slowrequesttimeout")]
        public required int SlowRequestTimeout { get; init; }

        [JsonPropertyName("cullingradius")]
        public required int CullingRadius { get; init; }

        // doesn't seem do anything
        [JsonPropertyName("commontapcount")]
        public required int CommonTapCount { get; init; }

        // doesn't seem do anything
        [JsonPropertyName("epictapcount")]
        public required int EpicTapCount { get; init; }

        [JsonPropertyName("speedwarningcooldown")]
        public required int SpeedWarningCooldown { get; init; }

        [JsonPropertyName("mintappablesrequiredpertile")]
        public required int MinTappablesRequiredPerTile { get; init; }

        [JsonPropertyName("targetactivetappables")]
        public required int TargetActiveTappables { get; init; }

        [JsonPropertyName("tappablecullingradius")]
        public required int TappableCullingRadius { get; init; }

        [JsonPropertyName("raretapcount")]
        public required int RareTapCount { get; init; }

        [JsonPropertyName("requestwarningtimeout")]
        public required int RequestWarningTimeout { get; init; }

        [JsonPropertyName("speedwarningthreshold")]
        public required float SpeedWarningThreshold { get; init; }

        [JsonPropertyName("asaanchormaxplaneheightthreshold")]
        public required float AsaAnchorMaxPlaneHeightThreshold { get; init; }

        [JsonPropertyName("maxannouncementscount")]
        public required int MaxAnnouncementsCount { get; init; }

        [JsonPropertyName("removethislater")]
        public required int RemoveThisLater { get; init; }

        [JsonPropertyName("crystalslotcap")]
        public required int CrystalSlotCap { get; init; }

        [JsonPropertyName("crystaluncommonduration")]
        public required int CrystalUncommonDuration { get; init; }

        [JsonPropertyName("crystalrareduration")]
        public required int CrystalRareDuration { get; init; }

        [JsonPropertyName("crystalepicduration")]
        public required int CrystalEpicDuration { get; init; }

        [JsonPropertyName("crystalcommonduration")]
        public required int CrystalCommonDuration { get; init; }

        [JsonPropertyName("crystallegendaryduration")]
        public required int CrystalLegendaryDuration { get; init; }

        [JsonPropertyName("maximumpersonaltimedchallenges")]
        public required int MaximumPersonalTimedChallenges { get; init; }

        [JsonPropertyName("maximumpersonalcontinuouschallenges")]
        public required int MaximumPersonalContinuousChallenges { get; init; }
    };

    // todo: make this configurable
    [HttpGet("features")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Endpoints cannot be static")]
    public EarthApiResponse<FeatureFlags> Features()
        => new(new()
        {
            WorkshopEnabled = true,
            BuildplatesEnabled = true,
            EnableRubyPurchasing = true,
            CommerceEnabled = true,
            FullLoggingEnabled = true,
            ChallengesEnabled = true,
            CraftingV2Enabled = true,
            SmeltingV2Enabled = true,
            InventoryItemBoostsEnabled = true,
            PlayerHealthEnabled = true,
            MinifigsEnabled = true,
            PotionsEnabled = true,
            SocialLinkLaunchEnabled = true,
            SocialLinkShareEnabled = true,
            EncodedJoinEnabled = true,
            AdventureCrystalsEnabled = true,
            ItemLimitsEnabled = true,
            AdventureCrystalsFtueEnabled = true,
            ExpireCrystalsOnCleanupEnabled = true,
            ChallengesV2Enabled = true,
            PlayerJournalEnabled = true,
            PlayerStatsEnabled = true,
            ActivityLogEnabled = true,
            SeasonsEnabled = true,
            DailyLoginEnabled = true,
            StorePdpEnabled = true,
            HotbarStacksplittingEnabled = true,
            FancyRewardsScreenEnabled = true,
            AsyncEcsDispatcher = true,
            AdventureOobeEnabled = true,
            TappableOobeEnabled = true,
            MapPermissionOobeEnabled = true,
            JournalOobeEnabled = true,
            FreedomOobeEnabled = true,
            ChallengeOobeEnabled = true,
            LevelRewardsV2Enabled = true,
            ContentDrivenSeasonAssets = true,
            PaidEarnedRubiesEnabled = true,
        });

    // todo: make this configurable
    [HttpGet("settings")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Endpoints cannot be static")]
    public EarthApiResponse<SettingsResponse> Settings()
        => new(new()
        {
            EncounterInteractionRadius = 40,
            TappableInteractionRadius = 70,
            TappableVisibleRadius = -5,
            TargetPossibleTappables = 100,
            Tile0 = 10537,
            SlowRequestTimeout = 3500,
            CullingRadius = 50,
            CommonTapCount = 3,
            RareTapCount = 5,
            EpicTapCount = 7,
            SpeedWarningCooldown = 3600,
            MinTappablesRequiredPerTile = 22,
            TargetActiveTappables = 30,
            TappableCullingRadius = 500,
            RequestWarningTimeout = 10000,
            SpeedWarningThreshold = 11.176f,
            AsaAnchorMaxPlaneHeightThreshold = 0.5f,
            MaxAnnouncementsCount = 0,
            RemoveThisLater = 23,
            CrystalSlotCap = 3,
            CrystalUncommonDuration = 10,
            CrystalRareDuration = 10,
            CrystalEpicDuration = 10,
            CrystalCommonDuration = 10,
            CrystalLegendaryDuration = 10,
            MaximumPersonalTimedChallenges = 3,
            MaximumPersonalContinuousChallenges = 3,
        });
}
