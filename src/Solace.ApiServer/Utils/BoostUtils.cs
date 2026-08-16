using System.Diagnostics;
using Solace.ApiServer.Types.Common;
using Solace.StaticData;

using CICIBIEActivation = Solace.StaticData.Catalog.ItemsCatalogR.Item.BoostEffectActivation;
using CICIBIEType = Solace.StaticData.Catalog.ItemsCatalogR.Item.BoostEffectType;

namespace Solace.ApiServer.Utils;

internal static class BoostUtils
{
    public static Effect BoostEffectToApiResponse(Catalog.ItemsCatalogR.Item.BoostEffect effect, TimeSpan boostDuration)
    {
        var effectTypeString = effect.Type switch
        {
            CICIBIEType.ADVENTURE_XP => "ItemExperiencePoints",
            CICIBIEType.CRAFTING => "CraftingSpeed",
            CICIBIEType.DEFENSE => "PlayerDefense",
            CICIBIEType.EATING => "FoodHealth",
            CICIBIEType.HEALING => "Health",
            CICIBIEType.HEALTH => "MaximumPlayerHealth",
            CICIBIEType.ITEM_XP => "ItemExperiencePoints",
            CICIBIEType.MINING_SPEED => "BlockDamage",
            CICIBIEType.RETENTION_BACKPACK => "RetainBackpack",
            CICIBIEType.RETENTION_HOTBAR => "RetainHotbar",
            CICIBIEType.RETENTION_XP => "RetainExperiencePoints",
            CICIBIEType.SMELTING => "SmeltingFuelIntensity",
            CICIBIEType.STRENGTH => "AttackDamage",
            CICIBIEType.TAPPABLE_RADIUS => "TappableInteractionRadius",
            _ => throw new UnreachableException(),
        };

        var activationString = effect.Activation switch
        {
            CICIBIEActivation.INSTANT => "Instant",
            CICIBIEActivation.TIMED => "Timed",
            CICIBIEActivation.TRIGGERED => "Triggered",
            _ => throw new UnreachableException(),
        };

        return new Effect(
            effectTypeString,
            effect.Activation == CICIBIEActivation.TIMED ? TimeFormatter.FormatDuration(boostDuration) : null,
            effect.Type is CICIBIEType.RETENTION_BACKPACK or CICIBIEType.RETENTION_HOTBAR or CICIBIEType.RETENTION_XP ? null : effect.Value,
            effect.Type switch
            {
                CICIBIEType.HEALING or CICIBIEType.TAPPABLE_RADIUS => "Increment",
                CICIBIEType.ADVENTURE_XP or CICIBIEType.CRAFTING or CICIBIEType.DEFENSE or CICIBIEType.EATING or CICIBIEType.HEALTH or CICIBIEType.ITEM_XP or CICIBIEType.MINING_SPEED or CICIBIEType.SMELTING or CICIBIEType.STRENGTH => "Percentage",
                CICIBIEType.RETENTION_BACKPACK or CICIBIEType.RETENTION_HOTBAR or CICIBIEType.RETENTION_XP => null,
                _ => throw new UnreachableException(),
            },
            effect.Type is CICIBIEType.CRAFTING or CICIBIEType.SMELTING ? "UtilityBlock" : "Player",
            effect.ApplicableItemIds,
            effect.Type switch
            {
                CICIBIEType.ITEM_XP => ["Tappable"],
                CICIBIEType.ADVENTURE_XP => ["Encounter"],
                _ => [],
            },
            activationString,
            effect.Type == CICIBIEType.EATING ? "Health" : null
        );
    }
}
