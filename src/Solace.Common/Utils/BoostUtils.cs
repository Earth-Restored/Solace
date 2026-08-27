using System.Diagnostics;
using Solace.Db.Earth.Models.Player;
using Solace.StaticData;

using CICIBIEActivation = Solace.StaticData.Catalog.ItemsCatalogR.Item.BoostEffectActivation;
using CICIBIEType = Solace.StaticData.Catalog.ItemsCatalogR.Item.BoostEffectType;

namespace Solace.Common.Utils;

internal static class BoostUtils
{
    // public static IEnumerable<Catalog.ItemsCatalogR.Item.BoostEffect> GetActiveEffects(BoostsEF boosts, DateTimeOffset currentTime, Catalog.ItemsCatalogR itemsCatalog)
    // {
    //     Dictionary<string, Catalog.ItemsCatalogR.Item.BoostInfoR> activeBoostsInfo = [];
    //     foreach (var activeBoost in boosts.ActiveBoosts)
    //     {
    //         if (activeBoost is null)
    //         {
    //             continue;
    //         }

    //         if (activeBoost.StartTime + activeBoost.Duration < currentTime)
    //         {
    //             continue;
    //         }

    //         var item = itemsCatalog.GetItem(activeBoost.ItemId);
    //         if (item is null || item.BoostInfo is null)
    //         {
    //             continue;
    //         }

    //         var existingBoostInfo = activeBoostsInfo.GetValueOrDefault(item.BoostInfo.Name);
    //         if (existingBoostInfo is not null && existingBoostInfo.Level > item.BoostInfo.Level)
    //         {
    //             continue;
    //         }

    //         activeBoostsInfo[item.BoostInfo.Name] = item.BoostInfo;
    //     }

    //     foreach (var boostInfo in activeBoostsInfo.Values)
    //     {
    //         foreach (var effect in boostInfo.Effects
    //             .Where(effect => effect.Activation switch
    //             {
    //                 CICIBIEActivation.INSTANT => false,
    //                 CICIBIEActivation.TRIGGERED => true,
    //                 CICIBIEActivation.TIMED => true, // already filtered for expiry time above
    //                 _ => throw new UnreachableException(),
    //             }))
    //         {
    //             yield return effect;
    //         }
    //     }
    // }

    public static IEnumerable<Catalog.ItemsCatalogR.Item.BoostEffect> GetActiveEffects(BoostsEF boosts, DateTimeOffset currentTime, Catalog catalog)
    {
        Dictionary<string, Catalog.ItemsCatalogR.Item.BoostInfoR> activeBoostsInfo = [with(StringComparer.Ordinal)];
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

            var item = catalog.ItemsCatalog.GetItem(activeBoost.ItemId);
            if (item is not null && item.BoostInfo is not null)
            {
                var existingBoostInfo = activeBoostsInfo.GetValueOrDefault(item.BoostInfo.Name);
                if (existingBoostInfo is not null && existingBoostInfo.Level > item.BoostInfo.Level)
                {
                    continue;
                }

                activeBoostsInfo[item.BoostInfo.Name] = item.BoostInfo;
            }
            else
            {
                var nfcBoost = catalog.NfcBoostsCatalog.MiniFigs.Values.FirstOrDefault(m => MiniFigIdTranslator.ToGuid(m.Id) == activeBoost.ItemId);
                if (nfcBoost is not null && nfcBoost.BoostMetadata is not null)
                {
                    var effects = nfcBoost.BoostMetadata.Effects.Select(effect =>
                    {
                        _ = Enum.TryParse<CICIBIEType>(effect.Type, true, out var effectType);
                        _ = Enum.TryParse<CICIBIEActivation>(effect.Activation, true, out var effectActivation);
                        return new Catalog.ItemsCatalogR.Item.BoostEffect(
                            effectType,
                            effect.Value is null ? 0 : (int)double.Round(effect.Value.Value),
                            effect.Items ?? [],
                            effectActivation
                        );
                    }).ToArray();

                    var boostInfo = new Catalog.ItemsCatalogR.Item.BoostInfoR(
                        nfcBoost.BoostMetadata.Name,
                        nfcBoost.BoostMetadata.Level,
                        Catalog.ItemsCatalogR.Item.BoostInfoType.POTION,
                        nfcBoost.BoostMetadata.CanBeRemoved,
                        0,
                        false,
                        effects
                    );

                    var existingBoostInfo = activeBoostsInfo.GetValueOrDefault(boostInfo.Name);
                    if (existingBoostInfo is not null && existingBoostInfo.Level > boostInfo.Level)
                    {
                        continue;
                    }

                    activeBoostsInfo[boostInfo.Name] = boostInfo;
                }
            }
        }

        foreach (var boostInfo in activeBoostsInfo.Values)
        {
            foreach (var effect in boostInfo.Effects
                .Where(effect => effect.Activation switch
                {
                    CICIBIEActivation.INSTANT => false,
                    CICIBIEActivation.TRIGGERED => true,
                    CICIBIEActivation.TIMED => true,
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

    public static StatModiferValues GetActiveStatModifiers(BoostsEF boosts, DateTimeOffset currentTime, Catalog catalog)
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

        foreach (var effect in BoostUtils.GetActiveEffects(boosts, currentTime, catalog))
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

    public static int GetMaxPlayerHealth(BoostsEF boosts, DateTimeOffset currentTime, Catalog catalog)
        => 20 + 20 * BoostUtils.GetActiveStatModifiers(boosts, currentTime, catalog).MaxPlayerHealthMultiplier / 100;
}
