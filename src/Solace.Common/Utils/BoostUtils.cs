using System.Diagnostics;
using Solace.Db.Earth.Models.Player;
using Solace.StaticData;

using CICIBIEActivation = Solace.StaticData.Catalog.ItemsCatalogR.Item.BoostEffectActivation;
using CICIBIEType = Solace.StaticData.Catalog.ItemsCatalogR.Item.BoostEffectType;

namespace Solace.Common.Utils;

internal static class BoostUtils
{
    public static IEnumerable<Catalog.ItemsCatalogR.Item.BoostEffect> GetActiveEffects(BoostsEF boosts, DateTimeOffset currentTime, Catalog.ItemsCatalogR itemsCatalog)
    {
        Dictionary<string, Catalog.ItemsCatalogR.Item.BoostInfoR> activeBoostsInfo = [];
        foreach (var activeBoost in boosts.ActiveBoosts)
        {
            if (activeBoost is null)
            {
                continue;
            }

            if (activeBoost.StartTime + activeBoost.Duration < currentTime)
            {
                continue;
            }

            Catalog.ItemsCatalogR.Item? item = itemsCatalog.GetItem(activeBoost.ItemId);
            if (item is null || item.BoostInfo is null)
            {
                continue;
            }

            Catalog.ItemsCatalogR.Item.BoostInfoR? existingBoostInfo = activeBoostsInfo.GetValueOrDefault(item.BoostInfo.Name);
            if (existingBoostInfo is not null && existingBoostInfo.Level > item.BoostInfo.Level)
            {
                continue;
            }

            activeBoostsInfo[item.BoostInfo.Name] = item.BoostInfo;
        }

        foreach (Catalog.ItemsCatalogR.Item.BoostInfoR boostInfo in activeBoostsInfo.Values)
        {
            foreach (var effect in boostInfo.Effects
                .Where(effect => effect.Activation switch
                {
                    CICIBIEActivation.INSTANT => false,
                    CICIBIEActivation.TRIGGERED => true,
                    CICIBIEActivation.TIMED => true, // already filtered for expiry time above
                    _ => throw new UnreachableException(),
                }))
            {
                yield return effect;
            }
        }
    }

    internal sealed record StatModiferValues(
        int MaxPlayerHealthMultiplier,
        int AttackMultiplier,
        int DefenseMultiplier,
        int FoodMultiplier,
        int MiningSpeedMultiplier,
        int CraftingSpeedMultiplier,
        int SmeltingSpeedMultiplier,
        int TappableInteractionRadiusExtraMeters,
        bool KeepHotbar,
        bool KeepInventory,
        bool KeepXp
    );

    public static StatModiferValues GetActiveStatModifiers(BoostsEF boosts, DateTimeOffset currentTime, Catalog.ItemsCatalogR itemsCatalog)
    {
        var maxPlayerHealth = 0;
        var attackMultiplier = 0;
        var defenseMultiplier = 0;
        var foodMultiplier = 0;
        var miningSpeedMultiplier = 0;
        var craftingMultiplier = 0;
        var smeltingMultiplier = 0;
        var tappableInteractionRadius = 0;
        var keepHotbar = false;
        var keepInventory = false;
        var keepXp = false;

        foreach (var effect in BoostUtils.GetActiveEffects(boosts, currentTime, itemsCatalog))
        {
            switch (effect.Type)
            {
                case CICIBIEType.HEALTH:
                    maxPlayerHealth += effect.Value;
                    break;
                case CICIBIEType.STRENGTH:
                    attackMultiplier += effect.Value;
                    break;
                case CICIBIEType.DEFENSE:
                    defenseMultiplier += effect.Value;
                    break;
                case CICIBIEType.EATING:
                    foodMultiplier += effect.Value;
                    break;
                case CICIBIEType.MINING_SPEED:
                    miningSpeedMultiplier += effect.Value;
                    break;
                case CICIBIEType.CRAFTING:
                    craftingMultiplier += effect.Value;
                    break;
                case CICIBIEType.SMELTING:
                    smeltingMultiplier += effect.Value;
                    break;
                case CICIBIEType.TAPPABLE_RADIUS:
                    tappableInteractionRadius += effect.Value;
                    break;
                case CICIBIEType.RETENTION_HOTBAR:
                    keepHotbar = true;
                    break;
                case CICIBIEType.RETENTION_BACKPACK:
                    keepInventory = true;
                    break;
                case CICIBIEType.RETENTION_XP:
                    keepXp = true;
                    break;
            }
        }

        return new StatModiferValues(
            maxPlayerHealth,
            attackMultiplier,
            defenseMultiplier,
            foodMultiplier,
            miningSpeedMultiplier,
            craftingMultiplier,
            smeltingMultiplier,
            tappableInteractionRadius,
            keepHotbar,
            keepInventory,
            keepXp
        );
    }

    public static int GetMaxPlayerHealth(BoostsEF boosts, DateTimeOffset currentTime, Catalog.ItemsCatalogR itemsCatalog)
        => 20 + (20 * BoostUtils.GetActiveStatModifiers(boosts, currentTime, itemsCatalog).MaxPlayerHealthMultiplier) / 100;
}
