using System.Diagnostics;
using Solace.Common.Utils;
using Solace.DB.Models.Player.Workshop;
using Solace.StaticData;

namespace Solace.ApiServer.Utils;

internal static class CraftingCalculator
{
    public static State CalculateState(DateTimeOffset currentTime, CraftingSlotEF.ActiveJobR activeJob, Catalog catalog)
    {
        Catalog.RecipesCatalogR.CraftingRecipe recipe = catalog.RecipesCatalog.Crafting.Where(craftingRecipe => craftingRecipe.Id == activeJob.RecipeId).First();

        var roundDuration = TimeSpan.FromSeconds(recipe.Duration);
        int completedRounds = activeJob.FinishedEarly ? activeJob.TotalRounds : int.Min((int)((currentTime - activeJob.StartTimeDT) / roundDuration), activeJob.TotalRounds);
        int availableRounds = completedRounds - activeJob.CollectedRounds;

        LinkedList<InputItem> input = [];
        if (activeJob.Input.Length != recipe.Ingredients.Length)
        {
            throw new InvalidOperationException();
        }

        for (int index = 0; index < recipe.Ingredients.Length; index++)
        {
            int usedCount = recipe.Ingredients[index].Count * completedRounds;
            InputItem[] inputItems = activeJob.Input[index].Items;
            foreach (InputItem inputItem in inputItems)
            {
                if (usedCount == 0)
                {
                    input.AddLast(inputItem);
                }
                else if (usedCount > inputItem.Count)
                {
                    usedCount -= inputItem.Count;
                }
                else
                {
                    if (inputItem.Instances.Length > 0)
                    {
                        if (inputItem.Instances.Length != inputItem.Count)
                        {
                            throw new UnreachableException();
                        }

                        input.AddLast(new InputItem(inputItem.Id, inputItem.Count - usedCount, inputItem.Instances[usedCount..]));
                    }
                    else
                    {
                        input.AddLast(new InputItem(inputItem.Id, inputItem.Count - usedCount, []));
                    }

                    usedCount = 0;
                }
            }
        }

        return new State(
            completedRounds,
            availableRounds,
            activeJob.TotalRounds,
            [.. input],
            new State.OutputItem(recipe.Output.ItemId, recipe.Output.Count),
            activeJob.StartTimeDT + roundDuration * (completedRounds + 1),
            activeJob.StartTimeDT + roundDuration * activeJob.TotalRounds,
            completedRounds == activeJob.TotalRounds
        );
    }

    internal sealed record State(
        int CompletedRounds,
        int AvailableRounds,
        int TotalRounds,
        InputItem[] Input,
        State.OutputItem Output,
        DateTimeOffset NextCompletionTime,
        DateTimeOffset TotalCompletionTime,
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

        int periods = (int)remainingTime.TotalSeconds / 10;
        if ((int)remainingTime.TotalSeconds % 10 > 0)
        {
            periods = periods + 1;
        }

        int price = periods * 5;
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
        => slotIndex < 1 || slotIndex > 3
        ? throw new ArgumentOutOfRangeException(nameof(slotIndex))
        : slotIndex * 5;
}
