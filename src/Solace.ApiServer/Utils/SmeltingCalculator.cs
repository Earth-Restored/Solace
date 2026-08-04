using System.Diagnostics;
using Solace.Db.Earth.Models.Player.Workshop;
using Solace.StaticData;

namespace Solace.ApiServer.Utils;

internal static class SmeltingCalculator
{
    public static State CalculateState(DateTimeOffset currentTime, SmeltingSlotEF.ActiveSmeltingJob activeJob, SmeltingSlotEF.BurningR? burning, Catalog catalog)
    {
        var recipe = catalog.RecipesCatalog.GetSmeltingRecipe(activeJob.RecipeId);
        Debug.Assert(recipe is not null);

        var totalHeatRequired = recipe.HeatRequired * activeJob.TotalRounds;
        var totalCompletionTime = activeJob.StartTime + CalculateDurationForHeat(totalHeatRequired, burning, activeJob.AddedFuel);
        var nextCompletionTime = DateTimeOffset.MinValue;
        int completedRounds;
        if (activeJob.FinishedEarly)
        {
            completedRounds = activeJob.TotalRounds;
        }
        else
        {
            for (completedRounds = 0; completedRounds < activeJob.TotalRounds; completedRounds++)
            {
                nextCompletionTime = activeJob.StartTime + CalculateDurationForHeat(recipe.HeatRequired * (completedRounds + 1), burning, activeJob.AddedFuel);
                if (nextCompletionTime >= currentTime)
                {
                    break;
                }
            }
        }

        if (completedRounds < activeJob.TotalRounds && nextCompletionTime == DateTimeOffset.MinValue)
        {
            throw new InvalidOperationException();
        }

        var availableRounds = completedRounds - activeJob.CollectedRounds;
        var completed = completedRounds == activeJob.TotalRounds;

        InputItem input;
        if (activeJob.Input.Count != activeJob.TotalRounds)
        {
            throw new InvalidOperationException();
        }

        if (activeJob.Input.Instances.Length > 0)
        {
            if (activeJob.Input.Instances.Length != activeJob.Input.Count)
            {
                throw new InvalidOperationException();
            }

            input = new InputItem(activeJob.Input.Id, activeJob.Input.Count - completedRounds, activeJob.Input.Instances[completedRounds..]);
        }
        else
        {
            input = new InputItem(activeJob.Input.Id, activeJob.Input.Count - completedRounds, []);
        }

        var consumedAddedFuelCount = 0;
        var fuelEndTime = completed ? totalCompletionTime : currentTime;
        SmeltingSlotEF.Fuel currentFuel;
        int currentFuelTotalHeat;
        DateTimeOffset burnStartTime;
        DateTimeOffset burnEndTime;

        if (burning is not null)
        {
            currentFuel = burning.Fuel;
            currentFuelTotalHeat = burning.RemainingHeat;
            burnStartTime = activeJob.StartTime;
            burnEndTime = burnStartTime + TimeSpan.FromMilliseconds(burning.RemainingHeat * 1000 / burning.Fuel.HeatPerSecond);
        }
        else
        {
            if (activeJob.AddedFuel is null)
            {
                throw new InvalidOperationException();
            }

            currentFuel = activeJob.AddedFuel;
            consumedAddedFuelCount = 1;
            currentFuelTotalHeat = currentFuel.HeatPerSecond * (int)currentFuel.BurnDuration.TotalSeconds;
            burnStartTime = activeJob.StartTime;
            burnEndTime = burnStartTime + currentFuel.BurnDuration;
        }

        while (burnEndTime < fuelEndTime)
        {
            if (activeJob.AddedFuel is null)
            {
                throw new InvalidOperationException();
            }

            totalHeatRequired -= currentFuelTotalHeat;
            currentFuel = activeJob.AddedFuel;
            consumedAddedFuelCount++;
            currentFuelTotalHeat = currentFuel.HeatPerSecond * (int)currentFuel.BurnDuration.TotalSeconds;
            burnStartTime = burnEndTime;
            burnEndTime = burnStartTime + currentFuel.BurnDuration;
        }

        if (totalHeatRequired < 0)
        {
            throw new InvalidOperationException();
        }

        int remainingHeat;
        if (!completed)
        {
            remainingHeat = (int)(((burnEndTime - fuelEndTime) * currentFuelTotalHeat) / currentFuel.BurnDuration);
        }
        else
        {
            if (totalHeatRequired > currentFuelTotalHeat)
            {
                throw new InvalidOperationException();
            }

            remainingHeat = currentFuelTotalHeat - totalHeatRequired;
        }

        SmeltingSlotEF.Fuel? remainingAddedFuel;
        if (activeJob.AddedFuel is null)
        {
            if (consumedAddedFuelCount > 0)
            {
                throw new InvalidOperationException();
            }

            remainingAddedFuel = null;
        }
        else
        {
            if (consumedAddedFuelCount > activeJob.AddedFuel.Item.Count)
            {
                throw new InvalidOperationException();
            }

            if (activeJob.AddedFuel.Item.Instances.Length > 0)
            {
                if (activeJob.AddedFuel.Item.Instances.Length != activeJob.AddedFuel.Item.Count)
                {
                    throw new InvalidOperationException();
                }

                remainingAddedFuel = new SmeltingSlotEF.Fuel(new InputItem(activeJob.AddedFuel.Item.Id, activeJob.AddedFuel.Item.Count - consumedAddedFuelCount, activeJob.AddedFuel.Item.Instances[consumedAddedFuelCount..]), activeJob.AddedFuel.BurnDuration, activeJob.AddedFuel.HeatPerSecond);
            }
            else
            {
                remainingAddedFuel = new SmeltingSlotEF.Fuel(new InputItem(activeJob.AddedFuel.Item.Id, activeJob.AddedFuel.Item.Count - consumedAddedFuelCount, []), activeJob.AddedFuel.BurnDuration, activeJob.AddedFuel.HeatPerSecond);
            }
        }

        SmeltingSlotEF.Fuel currentBurningFuel;
        if (consumedAddedFuelCount > 0)
        {
            if (activeJob.AddedFuel!.Item.Instances.Length > 0)
            {
                currentBurningFuel = new SmeltingSlotEF.Fuel(new InputItem(activeJob.AddedFuel.Item.Id, 1, [activeJob.AddedFuel.Item.Instances[consumedAddedFuelCount - 1]]), activeJob.AddedFuel.BurnDuration, activeJob.AddedFuel.HeatPerSecond);
            }
            else
            {
                currentBurningFuel = new SmeltingSlotEF.Fuel(new InputItem(activeJob.AddedFuel.Item.Id, 1, []), activeJob.AddedFuel.BurnDuration, activeJob.AddedFuel.HeatPerSecond);
            }
        }
        else
        {
            currentBurningFuel = currentFuel;
        }

        return new State(
            completedRounds,
            availableRounds,
            activeJob.TotalRounds,
            input,
            new State.OutputItem(recipe.Output, 1),
            nextCompletionTime,
            totalCompletionTime,
            remainingAddedFuel,
            currentBurningFuel,
            remainingHeat,
            burnStartTime,
            burnEndTime,
            completed
        );
    }

    private static TimeSpan CalculateDurationForHeat(int requiredHeat, SmeltingSlotEF.BurningR? burning, SmeltingSlotEF.Fuel? addedFuel)
    {
        var duration = TimeSpan.Zero;
        if (burning is not null)
        {
            if (burning.RemainingHeat >= requiredHeat)
            {
                duration += TimeSpan.FromMilliseconds(requiredHeat * 1000 / burning.Fuel.HeatPerSecond);
                requiredHeat = 0;
            }
            else
            {
                duration += TimeSpan.FromMilliseconds(burning.RemainingHeat * 1000 / burning.Fuel.HeatPerSecond);
                requiredHeat -= burning.RemainingHeat;
            }
        }

        if (addedFuel is not null)
        {
            for (var count = 0; count < addedFuel.Item.Count; count++)
            {
                if (requiredHeat < addedFuel.HeatPerSecond * addedFuel.BurnDuration.TotalSeconds)
                {
                    duration += TimeSpan.FromMilliseconds(requiredHeat * 1000 / addedFuel.HeatPerSecond);
                    requiredHeat = 0;
                    break;
                }
                else
                {
                    duration += addedFuel.BurnDuration;
                    requiredHeat -= addedFuel.HeatPerSecond * (int)addedFuel.BurnDuration.TotalSeconds;
                }
            }
        }

        if (requiredHeat > 0)
        {
            throw new InvalidOperationException();
        }

        return duration;
    }

    internal sealed record State(
        int CompletedRounds,
        int AvailableRounds,
        int TotalRounds,
        InputItem Input,
        State.OutputItem Output,
        DateTimeOffset NextCompletionTime,
        DateTimeOffset TotalCompletionTime,
        SmeltingSlotEF.Fuel? RemainingAddedFuel,
        SmeltingSlotEF.Fuel CurrentBurningFuel,
        int RemainingHeat,
        DateTimeOffset BurnStartTime,
        DateTimeOffset BurnEndTime,
        bool Completed
    )
    {
        internal sealed record OutputItem(
            Guid Id,
            int Count
        );
    }

    // TODO: make this configurable
    public static FinishPrice CalculateFinishPrice(TimeSpan remainingTime)
    {
        if (remainingTime < TimeSpan.Zero)
        {
            throw new ArgumentException($"{nameof(remainingTime)} is negative.", nameof(remainingTime));
        }

        var periods = (int)remainingTime.TotalSeconds / 10;
        if ((int)remainingTime.TotalSeconds % 10 > 0)
        {
            periods = periods + 1;
        }

        var price = periods * 5;
        var changesAt = TimeSpan.FromMilliseconds((periods - 1) * 10000);
        var validFor = remainingTime - changesAt;

        return new FinishPrice(price, validFor);
    }

    internal sealed record FinishPrice(
        int Price,
        TimeSpan ValidFor
    );

    // TODO: make this configurable
    public static int CalculateUnlockPrice(int slotIndex)
    {
        if (slotIndex < 1 || slotIndex > 3)
        {
            throw new ArgumentOutOfRangeException(nameof(slotIndex));
        }

        return slotIndex * 5;
    }
}
