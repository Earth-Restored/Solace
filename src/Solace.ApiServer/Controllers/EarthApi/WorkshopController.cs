using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Primitives;
using System.Diagnostics;
using Solace.ApiServer.Utils;
using Solace.Common.Excceptions;
using Solace.Common.Utils;
using Solace.StaticData;
using BurnRate = Solace.ApiServer.Types.Common.BurnRate;
using CraftingCalculator = Solace.ApiServer.Utils.CraftingCalculator;
using CraftingSlot = Solace.DB.Models.Player.Workshop.CraftingSlotEF;
using CraftingSlots = Solace.DB.Models.Player.Workshop.CraftingSlotsEF;
using EarthApiResponse = Solace.ApiServer.Utils.EarthApiResponse;
using ExpectedPurchasePriceR = Solace.ApiServer.Types.Common.ExpectedPurchasePriceR;
using FinishPrice = Solace.ApiServer.Types.Workshop.FinishPrice;
using Hotbar = Solace.DB.Models.Player.HotbarEF;
using InputItem = Solace.DB.Models.Player.Workshop.InputItem;
using Inventory = Solace.DB.Models.Player.InventoryEF;
using Journal = Solace.DB.Models.Player.JournalEF;
using NonStackableItemInstance = Solace.DB.Models.Common.NonStackableItemInstance;
using OutputItem = Solace.ApiServer.Types.Workshop.OutputItem;
using Profile = Solace.DB.Models.Player.ProfileEF;
using Rewards = Solace.ApiServer.Utils.Rewards;
using SmeltingCalculator = Solace.ApiServer.Utils.SmeltingCalculator;
using ActivityLog = Solace.DB.Models.Player.ActivityLogEF;
using SmeltingSlot = Solace.DB.Models.Player.Workshop.SmeltingSlot;
using SmeltingSlots = Solace.DB.Models.Player.Workshop.SmeltingSlotsEF;
using SplitRubies = Solace.ApiServer.Types.Profile.SplitRubies;
using State = Solace.ApiServer.Types.Workshop.State;
using TimeFormatter = Solace.ApiServer.Utils.TimeFormatter;
using UnlockPrice = Solace.ApiServer.Types.Workshop.UnlockPrice;
using Solace.DB;
using Microsoft.EntityFrameworkCore;
using Solace.DB.Utils;

namespace Solace.ApiServer.Controllers.EarthApi;

[Authorize]
[ApiVersion("1.1")]
[Route("1/api/v{version:apiVersion}")]
internal sealed class WorkshopRouter : SolaceControllerBase
{
    private readonly EarthDbContext earthDB;
    private readonly StaticData.StaticData staticData;

    public WorkshopRouter(EarthDbContext earthDb, StaticData.StaticData staticData)
    {
        earthDB = earthDb;
        this.staticData = staticData;
    }

    [HttpGet("player/utilityBlocks")]
    public async Task<Results<ContentHttpResult, BadRequest>> GetUtilityBlocks(CancellationToken cancellationToken)
    {
        if (!TryGetAccountId(out var accountId))
        {
            return TypedResults.BadRequest();
        }

        // request.timestamp
        long requestStartedOn = HttpContext.GetTimestamp();

        var craftingSlots = await earthDB.CraftingSlots
            .AsNoTracking()
            .FirstOrNewAsync(craftingSlots => craftingSlots.Id == accountId, cancellationToken: cancellationToken);

        var smeltingSlots = await earthDB.SmeltingSlots
            .AsNoTracking()
            .FirstOrNewAsync(smeltingSlots => smeltingSlots.Id == accountId, cancellationToken: cancellationToken);

        Dictionary<string, object> workshop = new()
        {
            ["crafting"] = new Dictionary<string, object>()
            {
                ["1"] = CraftingSlotModelToResponseIncludingLocked(craftingSlots.Slots[0], requestStartedOn, craftingSlots.Version, 1),
                ["2"] = CraftingSlotModelToResponseIncludingLocked(craftingSlots.Slots[1], requestStartedOn, craftingSlots.Version, 2),
                ["3"] = CraftingSlotModelToResponseIncludingLocked(craftingSlots.Slots[2], requestStartedOn, craftingSlots.Version, 3),
            },
            ["smelting"] = new Dictionary<string, object>()
            {
                ["1"] = SmeltingSlotModelToResponseIncludingLocked(smeltingSlots.Slots[0], requestStartedOn, smeltingSlots.Version, 1),
                ["2"] = SmeltingSlotModelToResponseIncludingLocked(smeltingSlots.Slots[1], requestStartedOn, smeltingSlots.Version, 2),
                ["3"] = SmeltingSlotModelToResponseIncludingLocked(smeltingSlots.Slots[2], requestStartedOn, smeltingSlots.Version, 3),
            },
        };

        return EarthJson(workshop);
    }

    [HttpGet("crafting/{slotIndex}")]
    public async Task<Results<ContentHttpResult, BadRequest>> GetCraftingStatus(int slotIndex, CancellationToken cancellationToken)
    {
        if (!TryGetAccountId(out var accountId) || slotIndex < 1 || slotIndex > 3)
        {
            return TypedResults.BadRequest();
        }

        // request.timestamp
        long requestStartedOn = HttpContext.GetTimestamp();

        var craftingSlots = await earthDB.CraftingSlots
            .AsNoTracking()
            .FirstOrNewAsync(craftingSlots => craftingSlots.Id == accountId, cancellationToken: cancellationToken);

        return EarthJson(CraftingSlotModelToResponseIncludingLocked(craftingSlots.Slots[slotIndex - 1], requestStartedOn, craftingSlots.Version, slotIndex));
    }

    [HttpGet("smelting/{slotIndex}")]
    public async Task<Results<ContentHttpResult, BadRequest>> GetSmeltingStatus(int slotIndex, CancellationToken cancellationToken)
    {
        if (!TryGetAccountId(out var accountId) || slotIndex < 1 || slotIndex > 3)
        {
            return TypedResults.BadRequest();
        }

        // request.timestamp
        long requestStartedOn = HttpContext.GetTimestamp();

        var smeltingSlots = await earthDB.SmeltingSlots
            .AsNoTracking()
            .FirstOrNewAsync(smeltingSlots => smeltingSlots.Id == accountId, cancellationToken: cancellationToken);

        return EarthJson(SmeltingSlotModelToResponseIncludingLocked(smeltingSlots.Slots[slotIndex - 1], requestStartedOn, smeltingSlots.Version, slotIndex));
    }

    [HttpPost("crafting/{slotIndex}/start")]
    public async Task<Results<ContentHttpResult, BadRequest>> StartCrafting(int slotIndex, CancellationToken cancellationToken)
    {
        if (!TryGetAccountId(out var accountId) || slotIndex < 1 || slotIndex > 3)
        {
            return TypedResults.BadRequest();
        }

        // request.timestamp
        long requestStartedOn = HttpContext.GetTimestamp();

        StartRequestCrafting? startRequest = await Request.Body.AsJsonAsync<StartRequestCrafting>(cancellationToken);
        if (startRequest is null || startRequest.Multiplier < 1)
        {
            return TypedResults.BadRequest();
        }

        if (startRequest.Ingredients.Any(item => item is null || item.Quantity < 1 || item.ItemInstanceIds is not null && item.ItemInstanceIds.Length > 0 && item.ItemInstanceIds.Length != item.Quantity))
        {
            return TypedResults.BadRequest();
        }

        Catalog.RecipesCatalogR.CraftingRecipe? recipe = staticData.Catalog.RecipesCatalog.GetCraftingRecipe(startRequest.RecipeId);

        if (recipe is null)
        {
            return TypedResults.BadRequest();
        }

        if (recipe.ReturnItems.Length > 0)
        {
            throw new UnsupportedOperationException(); // TODO: implement returnItems
        }

        var craftingSlots = await earthDB.CraftingSlots
            .AsTracking()
            .FirstOrNewAsync(craftingSlots => craftingSlots.Id == accountId, cancellationToken: cancellationToken);

        var inventory = await earthDB.Inventories
            .AsTracking()
            .FirstOrNewAsync(inventory => inventory.Id == accountId, cancellationToken: cancellationToken);

        var hotbar = await earthDB.Hotbars
            .AsTracking()
            .FirstOrNewAsync(hotbar => hotbar.Id == accountId, cancellationToken: cancellationToken);

        var craftingSlot = craftingSlots.Slots[slotIndex - 1];

        if (craftingSlot.Locked || craftingSlot.ActiveJob is not null)
        {
            return EarthJson(new Dictionary<string, object>(), new EarthApiResponse.UpdatesResponse());
        }

        var providedItems = new InputItem[startRequest.Ingredients.Length];
        for (int index = 0; index < startRequest.Ingredients.Length; index++)
        {
            StartRequestCrafting.Item item = startRequest.Ingredients[index];
            if (item.ItemInstanceIds is null || item.ItemInstanceIds.Length == 0)
            {
                if (!inventory.TakeItems(item.ItemId, item.Quantity))
                {
                    return EarthJson(new Dictionary<string, object>(), new EarthApiResponse.UpdatesResponse());
                }

                providedItems[index] = new InputItem(item.ItemId, item.Quantity, []);
            }
            else
            {
                NonStackableItemInstance[]? instances = inventory.TakeItems(item.ItemId, item.ItemInstanceIds);
                if (instances is null)
                {
                    return EarthJson(new Dictionary<string, object>(), new EarthApiResponse.UpdatesResponse());
                }

                providedItems[index] = new InputItem(item.ItemId, item.Quantity, instances);
            }
        }

        hotbar.LimitToInventory(inventory);

        LinkedList<LinkedList<InputItem>> inputItems = [];
        foreach (Catalog.RecipesCatalogR.CraftingRecipe.Ingredient ingredient in recipe.Ingredients)
        {
            LinkedList<InputItem> ingredientItems = [];
            int requiredCount = ingredient.Count * startRequest.Multiplier;
            for (int index = 0; index < providedItems.Length; index++)
            {
                InputItem providedItem = providedItems[index];
                if (providedItem.Count == 0)
                {
                    continue;
                }

                if (!ingredient.PossibleItemIds.Any(id => id == providedItem.Id))
                {
                    continue;
                }

                if (requiredCount > providedItem.Count)
                {
                    requiredCount -= providedItem.Count;
                    ingredientItems.AddLast(providedItem);
                    providedItems[index] = new InputItem(providedItem.Id, 0, []);
                }
                else
                {
                    NonStackableItemInstance[] takenInstances;
                    NonStackableItemInstance[] remainingInstances;
                    if (providedItem.Instances.Length > 0)
                    {
                        takenInstances = ArrayExtensions.CopyOfRange(providedItem.Instances, 0, requiredCount);
                        remainingInstances = ArrayExtensions.CopyOfRange(providedItem.Instances, requiredCount, providedItem.Count);
                    }
                    else
                    {
                        takenInstances = [];
                        remainingInstances = [];
                    }

                    ingredientItems.AddLast(new InputItem(providedItem.Id, requiredCount, takenInstances));
                    providedItems[index] = new InputItem(providedItem.Id, providedItem.Count - requiredCount, remainingInstances);
                    requiredCount = 0;
                }

                if (requiredCount == 0)
                {
                    break;
                }
            }

            if (requiredCount > 0)
            {
                return EarthJson(new Dictionary<string, object>(), new EarthApiResponse.UpdatesResponse());
            }

            if (ingredientItems.Count == 0)
            {
                throw new UnreachableException();
            }

            inputItems.AddLast(ingredientItems);
        }

        if (inputItems.Count != recipe.Ingredients.Length)
        {
            throw new UnreachableException();
        }

        if (providedItems.Any(item => item.Count > 0))
        {
            return EarthJson(new Dictionary<string, object>(), new EarthApiResponse.UpdatesResponse());
        }

        craftingSlot.ActiveJob = new CraftingSlot.ActiveJobR(startRequest.SessionId, recipe.Id, requestStartedOn, [.. inputItems.Select(inputItems1 => new CraftingSlot.InputRow([.. inputItems1]))], startRequest.Multiplier, 0, false);

        await earthDB.SaveChangesAsync(cancellationToken);

        return EarthJson(new Dictionary<string, object>(), new EarthApiResponse.UpdatesResponse(crafting: craftingSlots.Version, inventory: inventory.Version));
    }

    [HttpPost("smelting/{slotIndex}/start")]
    public async Task<Results<ContentHttpResult, BadRequest>> StartSmelting(int slotIndex, CancellationToken cancellationToken)
    {
        if (!TryGetAccountId(out var accountId) || slotIndex < 1 || slotIndex > 3)
        {
            return TypedResults.BadRequest();
        }

        // request.timestamp
        long requestStartedOn = HttpContext.GetTimestamp();

        StartRequestSmelting? startRequest = await Request.Body.AsJsonAsync<StartRequestSmelting>(cancellationToken);
        if (startRequest is null || startRequest.Multiplier < 1)
        {
            return TypedResults.BadRequest();
        }

        if (startRequest.Input.Quantity < 1 || startRequest.Input.ItemInstanceIds is not null && startRequest.Input.ItemInstanceIds.Length > 0 && startRequest.Input.ItemInstanceIds.Length != startRequest.Input.Quantity)
        {
            return TypedResults.BadRequest();
        }

        if (startRequest.Fuel is not null && startRequest.Fuel.Quantity > 0 && startRequest.Fuel.ItemInstanceIds is not null && startRequest.Fuel.ItemInstanceIds.Length > 0 && startRequest.Fuel.ItemInstanceIds.Length != startRequest.Fuel.Quantity)
        {
            return TypedResults.BadRequest();
        }

        Catalog.RecipesCatalogR.SmeltingRecipe? recipe = staticData.Catalog.RecipesCatalog.GetSmeltingRecipe(startRequest.RecipeId);
        Catalog.ItemsCatalogR.Item? fuelCatalogItem = startRequest.Fuel is not null ? staticData.Catalog.ItemsCatalog.GetItem(startRequest.Fuel.ItemId) : null;
        if (recipe is null)
        {
            return TypedResults.BadRequest();
        }

        if (startRequest.Fuel is not null && (fuelCatalogItem is null || fuelCatalogItem.FuelInfo is null))
        {
            return TypedResults.BadRequest();
        }

        if (recipe.ReturnItemId is not null)
        {
            throw new UnsupportedOperationException(); // TODO: implement returnItems
        }

        Debug.Assert(fuelCatalogItem is not null);
        Debug.Assert(fuelCatalogItem.FuelInfo is not null);

        if (startRequest.Fuel is not null && fuelCatalogItem.FuelInfo.ReturnItemId is not null)
        {
            throw new UnsupportedOperationException(); // TODO: implement returnItems
        }

        if (startRequest.Input.ItemId != recipe.Input || startRequest.Input.Quantity != startRequest.Multiplier)
        {
            return TypedResults.BadRequest();
        }

        var smeltingSlots = await earthDB.SmeltingSlots
            .AsTracking()
            .FirstOrNewAsync(smeltingSlots => smeltingSlots.Id == accountId, cancellationToken: cancellationToken);

        var inventory = await earthDB.Inventories
            .AsTracking()
            .FirstOrNewAsync(inventory => inventory.Id == accountId, cancellationToken: cancellationToken);

        var hotbar = await earthDB.Hotbars
            .AsTracking()
            .FirstOrNewAsync(hotbar => hotbar.Id == accountId, cancellationToken: cancellationToken);

        var smeltingSlot = smeltingSlots.Slots[slotIndex - 1];

        if (smeltingSlot.Locked || smeltingSlot.ActiveJob is not null)
        {
            return EarthJson(new Dictionary<string, object>(), new EarthApiResponse.UpdatesResponse());
        }

        InputItem input;
        if (startRequest.Input.ItemInstanceIds is null || startRequest.Input.ItemInstanceIds.Length == 0)
        {
            if (!inventory.TakeItems(startRequest.Input.ItemId, startRequest.Input.Quantity))
            {
                return EarthJson(new Dictionary<string, object>(), new EarthApiResponse.UpdatesResponse());
            }

            input = new InputItem(startRequest.Input.ItemId, startRequest.Input.Quantity, []);
        }
        else
        {
            NonStackableItemInstance[]? instances = inventory.TakeItems(startRequest.Input.ItemId, startRequest.Input.ItemInstanceIds);
            if (instances is null)
            {
                return EarthJson(new Dictionary<string, object>(), new EarthApiResponse.UpdatesResponse());
            }

            input = new InputItem(startRequest.Input.ItemId, startRequest.Input.Quantity, instances);
        }

        SmeltingSlot.Fuel? fuel;
        int requiredFuelHeat = recipe.HeatRequired * startRequest.Multiplier - (smeltingSlot.Burning is not null ? smeltingSlot.Burning.RemainingHeat : 0);
        if (startRequest.Fuel is not null && startRequest.Fuel.Quantity > 0)
        {
            int requiredFuelCount = 0;
            while (requiredFuelHeat > 0)
            {
                requiredFuelCount += 1;
                requiredFuelHeat -= fuelCatalogItem.FuelInfo.HeatPerSecond * fuelCatalogItem.FuelInfo.BurnTime;
            }

            if (startRequest.Fuel.Quantity < requiredFuelCount)
            {
                return EarthJson(new Dictionary<string, object>(), new EarthApiResponse.UpdatesResponse());
            }

            if (requiredFuelCount > 0)
            {
                InputItem fuelItem;
                if (startRequest.Fuel.ItemInstanceIds is null || startRequest.Fuel.ItemInstanceIds.Length == 0)
                {
                    if (!inventory.TakeItems(startRequest.Fuel.ItemId, requiredFuelCount))
                    {
                        return EarthJson(new Dictionary<string, object>(), new EarthApiResponse.UpdatesResponse());
                    }

                    fuelItem = new InputItem(startRequest.Fuel.ItemId, requiredFuelCount, []);
                }
                else
                {
                    NonStackableItemInstance[]? instances = inventory.TakeItems(startRequest.Fuel.ItemId, ArrayExtensions.CopyOfRange(startRequest.Fuel.ItemInstanceIds, 0, requiredFuelCount));
                    if (instances is null)
                    {
                        return EarthJson(new Dictionary<string, object>(), new EarthApiResponse.UpdatesResponse());
                    }

                    fuelItem = new InputItem(startRequest.Fuel.ItemId, requiredFuelCount, instances);
                }

                fuel = new SmeltingSlot.Fuel(fuelItem, fuelCatalogItem.FuelInfo.BurnTime, fuelCatalogItem.FuelInfo.HeatPerSecond);
            }
            else
            {
                fuel = null;
            }
        }
        else
        {
            if (requiredFuelHeat > 0)
            {
                return EarthJson(new Dictionary<string, object>(), new EarthApiResponse.UpdatesResponse());
            }

            fuel = null;
        }

        hotbar.LimitToInventory(inventory);

        smeltingSlot.ActiveJob = new SmeltingSlot.ActiveJobR(startRequest.SessionId, recipe.Id, requestStartedOn, input, fuel, startRequest.Multiplier, 0, false);

        await earthDB.SaveChangesAsync(cancellationToken);

        return EarthJson(new Dictionary<string, object>(), new EarthApiResponse.UpdatesResponse(smelting: smeltingSlots.Version, inventory: inventory.Version));
    }

    [HttpPost("crafting/{slotIndex}/collectItems")]
    public async Task<Results<ContentHttpResult, BadRequest>> CollectCraftingItems(int slotIndex, CancellationToken cancellationToken)
    {
        if (!TryGetAccountId(out var accountId) || slotIndex < 1 || slotIndex > 3)
        {
            return TypedResults.BadRequest();
        }

        // request.timestamp
        long requestStartedOn = HttpContext.GetTimestamp();

        var craftingSlots = await earthDB.CraftingSlots
            .AsTracking()
            .FirstOrNewAsync(craftingSlots => craftingSlots.Id == accountId, cancellationToken: cancellationToken);

        var craftingSlot = craftingSlots.Slots[slotIndex - 1];

        var rewards = new Rewards();
        if (craftingSlot.ActiveJob is not null)
        {
            CraftingCalculator.State state = CraftingCalculator.CalculateState(requestStartedOn, craftingSlot.ActiveJob, staticData.Catalog);

            int quantity = state.AvailableRounds * state.Output.Count;
            if (quantity > 0)
            {
                rewards.AddItem(state.Output.Id, quantity);
            }

            if (state.Completed)
            {
                craftingSlot.ActiveJob = null;
            }
            else
            {
                CraftingSlot.ActiveJobR activeJob = craftingSlot.ActiveJob;
                craftingSlot.ActiveJob = new CraftingSlot.ActiveJobR(activeJob.SessionId, activeJob.RecipeId, activeJob.StartTime, activeJob.Input, activeJob.TotalRounds, activeJob.CollectedRounds + state.AvailableRounds, activeJob.FinishedEarly);
            }
        }

        await earthDB.SaveChangesAsync(cancellationToken);

        var results = new EarthDbContext.Results(earthDB);
        results.Crafting = craftingSlots.Version;

        await ActivityLogUtils.AddEntryAsync(results, accountId, new ActivityLog.CraftingCompletedEntry(requestStartedOn, rewards.ToDBRewardsModel()));
        await rewards.ToRedeemQueryAsync(results, accountId, requestStartedOn, staticData);

        return EarthJson(new Dictionary<string, object>()
            {
                { "rewards", rewards.ToApiResponse() }
            }, new EarthApiResponse.UpdatesResponse(results));
    }

    [HttpPost("smelting/{slotIndex}/collectItems")]
    public async Task<Results<ContentHttpResult, BadRequest>> CollectSmeltingItems(int slotIndex, CancellationToken cancellationToken)
    {
        if (!TryGetAccountId(out var accountId) || slotIndex < 1 || slotIndex > 3)
        {
            return TypedResults.BadRequest();
        }

        // request.timestamp
        long requestStartedOn = HttpContext.GetTimestamp();

        var smeltingSlots = await earthDB.SmeltingSlots
            .AsTracking()
            .FirstOrNewAsync(smeltingSlots => smeltingSlots.Id == accountId, cancellationToken: cancellationToken);

        var smeltingSlot = smeltingSlots.Slots[slotIndex - 1];

        var rewards = new Rewards();
        if (smeltingSlot.ActiveJob is not null)
        {
            SmeltingCalculator.State state = SmeltingCalculator.CalculateState(requestStartedOn, smeltingSlot.ActiveJob, smeltingSlot.Burning, staticData.Catalog);

            int quantity = state.AvailableRounds * state.Output.Count;
            if (quantity > 0)
            {
                rewards.AddItem(state.Output.Id, quantity);
            }

            if (state.Completed)
            {
                smeltingSlot.ActiveJob = null;
                if (state.RemainingHeat > 0)
                {
                    smeltingSlot.Burning = new SmeltingSlot.BurningR(
                        state.CurrentBurningFuel,
                        state.RemainingHeat
                    );
                }
                else
                {
                    smeltingSlot.Burning = null;
                }
            }
            else
            {
                SmeltingSlot.ActiveJobR activeJob = smeltingSlot.ActiveJob;
                smeltingSlot.ActiveJob = new SmeltingSlot.ActiveJobR(activeJob.SessionId, activeJob.RecipeId, activeJob.StartTime, activeJob.Input, activeJob.AddedFuel, activeJob.TotalRounds, activeJob.CollectedRounds + state.AvailableRounds, activeJob.FinishedEarly);
            }
        }

        await earthDB.SaveChangesAsync(cancellationToken);

        var results = new EarthDbContext.Results(earthDB);
        results.Smelting = smeltingSlots.Version;

        await ActivityLogUtils.AddEntryAsync(results, accountId, new ActivityLog.SmeltingCompletedEntry(requestStartedOn, rewards.ToDBRewardsModel()));
        await rewards.ToRedeemQueryAsync(results, accountId, requestStartedOn, staticData);

        return EarthJson(new Dictionary<string, object>()
            {
                { "rewards", rewards.ToApiResponse() }
            }, new EarthApiResponse.UpdatesResponse(results));
    }

    [HttpPost("crafting/{slotIndex}/stop")]
    public async Task<Results<ContentHttpResult, BadRequest>> StopCraftingJob(int slotIndex, CancellationToken cancellationToken)
    {
        if (!TryGetAccountId(out var accountId) || slotIndex < 1 || slotIndex > 3)
        {
            return TypedResults.BadRequest();
        }

        // request.timestamp
        long requestStartedOn = HttpContext.GetTimestamp();

        var craftingSlots = await earthDB.CraftingSlots
            .AsTracking()
            .FirstOrNewAsync(craftingSlots => craftingSlots.Id == accountId, cancellationToken: cancellationToken);

        var inventory = await earthDB.Inventories
            .AsTracking()
            .FirstOrNewAsync(inventory => inventory.Id == accountId, cancellationToken: cancellationToken);

        var journal = await earthDB.Journals
            .AsTracking()
            .FirstOrNewAsync(journal => journal.Id == accountId, cancellationToken: cancellationToken);

        var craftingSlot = craftingSlots.Slots[slotIndex - 1];

        if (craftingSlot.ActiveJob is null)
        {
            return EarthJson(CraftingSlotModelToResponse(craftingSlot, requestStartedOn, craftingSlots.Version));
        }

        CraftingCalculator.State state = CraftingCalculator.CalculateState(requestStartedOn, craftingSlot.ActiveJob, staticData.Catalog);

        foreach (InputItem inputItem in state.Input)
        {
            if (inputItem.Instances.Length > 0)
            {
                inventory.AddItems(inputItem.Id, [.. inputItem.Instances.Select(instance => new NonStackableItemInstance(instance.InstanceId, instance.Wear))]);
            }
            else if (inputItem.Count > 0)
            {
                inventory.AddItems(inputItem.Id, inputItem.Count);
            }

            journal.AddCollectedItem(inputItem.Id, requestStartedOn, 0);
        }

        var rewards = new Rewards();
        int outputQuantity = state.AvailableRounds * state.Output.Count;
        if (outputQuantity > 0)
        {
            rewards.AddItem(state.Output.Id, outputQuantity);
        }

        craftingSlot.ActiveJob = null;

        await earthDB.SaveChangesAsync(cancellationToken);

        var results = new EarthDbContext.Results(earthDB);
        results.Crafting = craftingSlots.Version;
        results.Inventory = inventory.Version;
        results.Journal = journal.Version;

        await ActivityLogUtils.AddEntryAsync(results, accountId, new ActivityLog.CraftingCompletedEntry(requestStartedOn, rewards.ToDBRewardsModel()));
        await rewards.ToRedeemQueryAsync(results, accountId, requestStartedOn, staticData);

        return EarthJson(CraftingSlotModelToResponse(craftingSlot, requestStartedOn, craftingSlots.Version), new EarthApiResponse.UpdatesResponse(results));
    }

    [HttpPost("smelting/{slotIndex}/stop")]
    public async Task<Results<ContentHttpResult, BadRequest>> StopSmeltingJob(int slotIndex, CancellationToken cancellationToken)
    {
        if (!TryGetAccountId(out var accountId) || slotIndex < 1 || slotIndex > 3)
        {
            return TypedResults.BadRequest();
        }

        // request.timestamp
        long requestStartedOn = HttpContext.GetTimestamp();

        var smeltingSlots = await earthDB.SmeltingSlots
            .AsTracking()
            .FirstOrNewAsync(smeltingSlots => smeltingSlots.Id == accountId, cancellationToken: cancellationToken);

        var inventory = await earthDB.Inventories
            .AsTracking()
            .FirstOrNewAsync(inventory => inventory.Id == accountId, cancellationToken: cancellationToken);

        var journal = await earthDB.Journals
            .AsTracking()
            .FirstOrNewAsync(journal => journal.Id == accountId, cancellationToken: cancellationToken);

        var smeltingSlot = smeltingSlots.Slots[slotIndex - 1];

        if (smeltingSlot.ActiveJob is null)
        {
            return EarthJson(SmeltingSlotModelToResponse(smeltingSlot, requestStartedOn, smeltingSlots.Version), new EarthApiResponse.UpdatesResponse());
        }

        SmeltingCalculator.State state = SmeltingCalculator.CalculateState(requestStartedOn, smeltingSlot.ActiveJob, smeltingSlot.Burning, staticData.Catalog);

        if (state.Input.Instances.Length > 0)
        {
            inventory.AddItems(state.Input.Id, [.. state.Input.Instances.Select(instance => new NonStackableItemInstance(instance.InstanceId, instance.Wear))]);
        }
        else if (state.Input.Count > 0)
        {
            inventory.AddItems(state.Input.Id, state.Input.Count);
        }

        journal.AddCollectedItem(state.Input.Id, requestStartedOn, 0);

        if (state.RemainingAddedFuel is not null)
        {
            if (state.RemainingAddedFuel.Item.Instances.Length > 0)
            {
                inventory.AddItems(state.RemainingAddedFuel.Item.Id, [.. state.RemainingAddedFuel.Item.Instances.Select(instance => new NonStackableItemInstance(instance.InstanceId, instance.Wear))]);
            }
            else if (state.RemainingAddedFuel.Item.Count > 0)
            {
                inventory.AddItems(state.RemainingAddedFuel.Item.Id, state.RemainingAddedFuel.Item.Count);
            }

            journal.AddCollectedItem(state.RemainingAddedFuel.Item.Id, requestStartedOn, 0);
        }

        var rewards = new Rewards();
        int outputQuantity = state.AvailableRounds * state.Output.Count;
        if (outputQuantity > 0)
        {
            rewards.AddItem(state.Output.Id, outputQuantity);
        }

        smeltingSlot.ActiveJob = null;
        if (state.RemainingHeat > 0)
        {
            smeltingSlot.Burning = new SmeltingSlot.BurningR(state.CurrentBurningFuel, state.RemainingHeat);
        }
        else
        {
            smeltingSlot.Burning = null;
        }

        await earthDB.SaveChangesAsync(cancellationToken);

        var results = new EarthDbContext.Results(earthDB);
        results.Smelting = smeltingSlots.Version;
        results.Inventory = inventory.Version;
        results.Journal = journal.Version;

        await ActivityLogUtils.AddEntryAsync(results, accountId, new ActivityLog.SmeltingCompletedEntry(requestStartedOn, rewards.ToDBRewardsModel()));
        await rewards.ToRedeemQueryAsync(results, accountId, requestStartedOn, staticData);

        return EarthJson(SmeltingSlotModelToResponse(smeltingSlot, requestStartedOn, smeltingSlots.Version), new EarthApiResponse.UpdatesResponse(results));
    }

    [HttpPost("crafting/{slotIndex}/finish")]
    public async Task<Results<ContentHttpResult, BadRequest>> FinishCrafting(int slotIndex, CancellationToken cancellationToken)
    {
        if (!TryGetAccountId(out var accountId) || slotIndex < 1 || slotIndex > 3)
        {
            return TypedResults.BadRequest();
        }

        // request.timestamp
        long requestStartedOn = HttpContext.GetTimestamp();

        ExpectedPurchasePriceR? expectedPurchasePrice = await Request.Body.AsJsonAsync<ExpectedPurchasePriceR>(cancellationToken);
        if (expectedPurchasePrice is null || expectedPurchasePrice.ExpectedPurchasePrice < 0)
        {
            return TypedResults.BadRequest();
        }

        var craftingSlots = await earthDB.CraftingSlots
            .AsTracking()
            .FirstOrNewAsync(craftingSlots => craftingSlots.Id == accountId, cancellationToken: cancellationToken);

        var profile = await earthDB.Profiles
            .AsTracking()
            .FirstOrNewAsync(profile => profile.Id == accountId, cancellationToken: cancellationToken);

        var craftingSlot = craftingSlots.Slots[slotIndex - 1];

        if (craftingSlot.ActiveJob is null)
        {
            return EarthJson(new SplitRubies(profile.Rubies.Purchased, profile.Rubies.Earned), new EarthApiResponse.UpdatesResponse());
        }

        CraftingCalculator.State state = CraftingCalculator.CalculateState(requestStartedOn, craftingSlot.ActiveJob, staticData.Catalog);
        if (state.Completed)
        {
            return EarthJson(new SplitRubies(profile.Rubies.Purchased, profile.Rubies.Earned), new EarthApiResponse.UpdatesResponse());
        }

        int remainingTime = (int)(state.TotalCompletionTime - requestStartedOn);
        if (remainingTime < 0)
        {
            return EarthJson(new SplitRubies(profile.Rubies.Purchased, profile.Rubies.Earned), new EarthApiResponse.UpdatesResponse());
        }

        CraftingCalculator.FinishPrice finishPrice = CraftingCalculator.CalculateFinishPrice(remainingTime);

        if (expectedPurchasePrice.ExpectedPurchasePrice < finishPrice.Price)
        {
            return EarthJson(new SplitRubies(profile.Rubies.Purchased, profile.Rubies.Earned), new EarthApiResponse.UpdatesResponse());
        }

        if (!profile.Rubies.Spend(finishPrice.Price))
        {
            return EarthJson(new SplitRubies(profile.Rubies.Purchased, profile.Rubies.Earned), new EarthApiResponse.UpdatesResponse());
        }

        CraftingSlot.ActiveJobR activeJob = craftingSlot.ActiveJob;
        craftingSlot.ActiveJob = new CraftingSlot.ActiveJobR(activeJob.SessionId, activeJob.RecipeId, activeJob.StartTime, activeJob.Input, activeJob.TotalRounds, activeJob.CollectedRounds, true);

        await earthDB.SaveChangesAsync(cancellationToken);

        var results = new EarthDbContext.Results(earthDB);
        results.Crafting = craftingSlots.Version;
        results.Profile = profile.Version;

        return EarthJson(new SplitRubies(profile.Rubies.Purchased, profile.Rubies.Earned), new EarthApiResponse.UpdatesResponse(results));
    }

    [HttpPost("smelting/{slotIndex}/finish")]
    public async Task<Results<ContentHttpResult, BadRequest>> FinishSmelting(int slotIndex, CancellationToken cancellationToken)
    {
        if (!TryGetAccountId(out var accountId) || slotIndex < 1 || slotIndex > 3)
        {
            return TypedResults.BadRequest();
        }

        // request.timestamp
        long requestStartedOn = HttpContext.GetTimestamp();

        ExpectedPurchasePriceR? expectedPurchasePrice = await Request.Body.AsJsonAsync<ExpectedPurchasePriceR>(cancellationToken);
        if (expectedPurchasePrice is null || expectedPurchasePrice.ExpectedPurchasePrice < 0)
        {
            return TypedResults.BadRequest();
        }

        var smeltingSlots = await earthDB.SmeltingSlots
                .AsTracking()
                .FirstOrNewAsync(smeltingSlots => smeltingSlots.Id == accountId, cancellationToken: cancellationToken);

        var profile = await earthDB.Profiles
            .AsTracking()
            .FirstOrNewAsync(profile => profile.Id == accountId, cancellationToken: cancellationToken);

        var smeltingSlot = smeltingSlots.Slots[slotIndex - 1];

        if (smeltingSlot.ActiveJob is null)
        {
            return EarthJson(new SplitRubies(profile.Rubies.Purchased, profile.Rubies.Earned), new EarthApiResponse.UpdatesResponse());
        }

        SmeltingCalculator.State state = SmeltingCalculator.CalculateState(requestStartedOn, smeltingSlot.ActiveJob, smeltingSlot.Burning, staticData.Catalog);
        if (state.Completed)
        {
            return EarthJson(new SplitRubies(profile.Rubies.Purchased, profile.Rubies.Earned), new EarthApiResponse.UpdatesResponse());
        }

        int remainingTime = (int)(state.TotalCompletionTime - requestStartedOn);
        if (remainingTime < 0)
        {
            return EarthJson(new SplitRubies(profile.Rubies.Purchased, profile.Rubies.Earned), new EarthApiResponse.UpdatesResponse());
        }

        SmeltingCalculator.FinishPrice finishPrice = SmeltingCalculator.CalculateFinishPrice(remainingTime);

        if (expectedPurchasePrice.ExpectedPurchasePrice < finishPrice.Price)
        {
            return EarthJson(new SplitRubies(profile.Rubies.Purchased, profile.Rubies.Earned), new EarthApiResponse.UpdatesResponse());
        }

        if (!profile.Rubies.Spend(finishPrice.Price))
        {
            return EarthJson(new SplitRubies(profile.Rubies.Purchased, profile.Rubies.Earned), new EarthApiResponse.UpdatesResponse());
        }

        SmeltingSlot.ActiveJobR activeJob = smeltingSlot.ActiveJob;
        smeltingSlot.ActiveJob = new SmeltingSlot.ActiveJobR(activeJob.SessionId, activeJob.RecipeId, activeJob.StartTime, activeJob.Input, activeJob.AddedFuel, activeJob.TotalRounds, activeJob.CollectedRounds, true);

        await earthDB.SaveChangesAsync(cancellationToken);

        var results = new EarthDbContext.Results(earthDB);
        results.Smelting = smeltingSlots.Version;
        results.Profile = profile.Version;

        return EarthJson(new SplitRubies(profile.Rubies.Purchased, profile.Rubies.Earned), new EarthApiResponse.UpdatesResponse(results));
    }

    [HttpGet("crafting/finish/price")]
    public Results<ContentHttpResult, BadRequest> GetCraftingPrice()
    {
        if (!Request.Query.TryGetValue("remainingTime", out StringValues remainingTimeString))
        {
            return TypedResults.BadRequest();
        }

        int remainingTime;
        try
        {
            remainingTime = (int)TimeFormatter.ParseDuration(remainingTimeString.ToString());
            if (remainingTime < 0)
            {
                return TypedResults.BadRequest();
            }
        }
        catch
        {
            return TypedResults.BadRequest();
        }

        CraftingCalculator.FinishPrice finishPrice = CraftingCalculator.CalculateFinishPrice(remainingTime);

        return EarthJson(new FinishPrice(finishPrice.Price, 0, TimeFormatter.FormatDuration(finishPrice.ValidFor)));
    }

    [HttpGet("smelting/finish/price")]
    public Results<ContentHttpResult, BadRequest> GetSmeltingPrice()
    {
        if (!Request.Query.TryGetValue("remainingTime", out StringValues remainingTimeString))
        {
            return TypedResults.BadRequest();
        }

        int remainingTime;
        try
        {
            remainingTime = (int)TimeFormatter.ParseDuration(remainingTimeString.ToString());
            if (remainingTime < 0)
            {
                return TypedResults.BadRequest();
            }
        }
        catch
        {
            return TypedResults.BadRequest();
        }

        SmeltingCalculator.FinishPrice finishPrice = SmeltingCalculator.CalculateFinishPrice(remainingTime);

        return EarthJson(new FinishPrice(finishPrice.Price, 0, TimeFormatter.FormatDuration(finishPrice.ValidFor)));
    }

    [HttpPost("crafting/{slotIndex}/unlock")]
    public async Task<Results<ContentHttpResult, BadRequest>> UnlockCraftingSlot(int slotIndex, CancellationToken cancellationToken)
    {
        if (!TryGetAccountId(out var accountId) || slotIndex < 1 || slotIndex > 3)
        {
            return TypedResults.BadRequest();
        }

        ExpectedPurchasePriceR? expectedPurchasePrice = await Request.Body.AsJsonAsync<ExpectedPurchasePriceR>(cancellationToken);
        if (expectedPurchasePrice is null || expectedPurchasePrice.ExpectedPurchasePrice < 0)
        {
            return TypedResults.BadRequest();
        }

        var craftingSlots = await earthDB.CraftingSlots
                  .AsTracking()
                  .FirstOrNewAsync(craftingSlots => craftingSlots.Id == accountId, cancellationToken: cancellationToken);

        var profile = await earthDB.Profiles
            .AsTracking()
            .FirstOrNewAsync(profile => profile.Id == accountId, cancellationToken: cancellationToken);

        var craftingSlot = craftingSlots.Slots[slotIndex - 1];

        if (!craftingSlot.Locked)
        {
            return EarthJson(new Dictionary<string, object>(), new EarthApiResponse.UpdatesResponse());
        }

        int unlockPrice = CraftingCalculator.CalculateUnlockPrice(slotIndex);

        if (expectedPurchasePrice.ExpectedPurchasePrice != unlockPrice)
        {
            return EarthJson(new Dictionary<string, object>(), new EarthApiResponse.UpdatesResponse());
        }

        if (!profile.Rubies.Spend(unlockPrice))
        {
            return EarthJson(new Dictionary<string, object>(), new EarthApiResponse.UpdatesResponse());
        }

        craftingSlot.Locked = false;

        await earthDB.SaveChangesAsync(cancellationToken);

        var results = new EarthDbContext.Results(earthDB);
        results.Crafting = craftingSlots.Version;
        results.Profile = profile.Version;

        return EarthJson(new Dictionary<string, object>(), new EarthApiResponse.UpdatesResponse(results));
    }

    [HttpPost("smelting/{slotIndex}/unlock")]
    public async Task<Results<ContentHttpResult, BadRequest>> UnlockSmeltingSlot(int slotIndex, CancellationToken cancellationToken)
    {
        if (!TryGetAccountId(out var accountId) || slotIndex < 1 || slotIndex > 3)
        {
            return TypedResults.BadRequest();
        }

        ExpectedPurchasePriceR? expectedPurchasePrice = await Request.Body.AsJsonAsync<ExpectedPurchasePriceR>(cancellationToken);
        if (expectedPurchasePrice is null || expectedPurchasePrice.ExpectedPurchasePrice < 0)
        {
            return TypedResults.BadRequest();
        }

        var smeltingSlots = await earthDB.SmeltingSlots
                     .AsTracking()
                     .FirstOrNewAsync(smeltingSlots => smeltingSlots.Id == accountId, cancellationToken: cancellationToken);

        var profile = await earthDB.Profiles
            .AsTracking()
            .FirstOrNewAsync(profile => profile.Id == accountId, cancellationToken: cancellationToken);

        var smeltingSlot = smeltingSlots.Slots[slotIndex - 1];

        if (!smeltingSlot.Locked)
        {
            return EarthJson(new Dictionary<string, object>(), new EarthApiResponse.UpdatesResponse());
        }

        int unlockPrice = SmeltingCalculator.CalculateUnlockPrice(slotIndex);

        if (expectedPurchasePrice.ExpectedPurchasePrice != unlockPrice)
        {
            return EarthJson(new Dictionary<string, object>(), new EarthApiResponse.UpdatesResponse());
        }

        if (!profile.Rubies.Spend(unlockPrice))
        {
            return EarthJson(new Dictionary<string, object>(), new EarthApiResponse.UpdatesResponse());
        }

        smeltingSlot.Locked = false;

        await earthDB.SaveChangesAsync(cancellationToken);

        var results = new EarthDbContext.Results(earthDB);
        results.Smelting = smeltingSlots.Version;
        results.Profile = profile.Version;

        return EarthJson(new Dictionary<string, object>(), new EarthApiResponse.UpdatesResponse(results));
    }

    private Types.Workshop.CraftingSlot CraftingSlotModelToResponseIncludingLocked(CraftingSlot craftingSlotModel, long currentTime, int streamVersion, int slotIndex)
    {
        if (craftingSlotModel.Locked)
        {
            return new Types.Workshop.CraftingSlot(null, null, null, null, 0, 0, 0, null, null, State.LOCKED, null, new UnlockPrice(CraftingCalculator.CalculateUnlockPrice(slotIndex), 0), streamVersion);
        }
        else
        {
            return CraftingSlotModelToResponse(craftingSlotModel, currentTime, streamVersion);
        }
    }

    private Types.Workshop.CraftingSlot CraftingSlotModelToResponse(CraftingSlot craftingSlotModel, long currentTime, int streamVersion)
    {
        if (craftingSlotModel.Locked)
        {
            throw new ArgumentException($"{nameof(craftingSlotModel)} is locked.", nameof(craftingSlotModel));
        }

        CraftingSlot.ActiveJobR? activeJob = craftingSlotModel.ActiveJob;
        if (activeJob is not null)
        {
            CraftingCalculator.State state = CraftingCalculator.CalculateState(currentTime, activeJob, staticData.Catalog);
            return new Types.Workshop.CraftingSlot(
                activeJob.SessionId,
                activeJob.RecipeId,
                new OutputItem(state.Output.Id, state.Output.Count),
                [.. activeJob.Input.SelectMany(inputItems => inputItems.Items).Select(item => new Types.Workshop.InputItem(
                    item.Id,
                    item.Count,
                    [.. item.Instances.Select(item => item.InstanceId)]
                ))],
                state.CompletedRounds,
                state.AvailableRounds,
                state.TotalRounds,
                !state.Completed ? TimeFormatter.FormatTime(state.NextCompletionTime) : null,
                !state.Completed ? TimeFormatter.FormatTime(state.TotalCompletionTime) : null,
                state.Completed ? State.COMPLETED : State.ACTIVE,
                null,
                null,
                streamVersion
            );
        }
        else
        {
            return new Types.Workshop.CraftingSlot(null, null, null, null, 0, 0, 0, null, null, State.EMPTY, null, null, streamVersion);
        }
    }

    private Types.Workshop.SmeltingSlot SmeltingSlotModelToResponseIncludingLocked(SmeltingSlot smeltingSlotModel, long currentTime, int streamVersion, int slotIndex)
    {
        if (smeltingSlotModel.Locked)
        {
            return new Types.Workshop.SmeltingSlot(null, null, null, null, null, null, 0, 0, 0, null, null, State.LOCKED, null, new UnlockPrice(SmeltingCalculator.CalculateUnlockPrice(slotIndex), 0), streamVersion);
        }
        else
        {
            return SmeltingSlotModelToResponse(smeltingSlotModel, currentTime, streamVersion);
        }
    }

    private Types.Workshop.SmeltingSlot SmeltingSlotModelToResponse(SmeltingSlot smeltingSlotModel, long currentTime, int streamVersion)
    {
        if (smeltingSlotModel.Locked)
        {
            throw new ArgumentException($"{nameof(smeltingSlotModel)} is locked.", nameof(smeltingSlotModel));
        }

        SmeltingSlot.ActiveJobR? activeJob = smeltingSlotModel.ActiveJob;
        if (activeJob is not null)
        {
            SmeltingCalculator.State state = SmeltingCalculator.CalculateState(currentTime, activeJob, smeltingSlotModel.Burning, staticData.Catalog);

            Types.Workshop.SmeltingSlot.FuelR? fuel;
            if (state.RemainingAddedFuel is not null && state.RemainingAddedFuel.Item.Count > 0)
            {
                fuel = new Types.Workshop.SmeltingSlot.FuelR(
                    new BurnRate(state.RemainingAddedFuel.BurnDuration, state.RemainingAddedFuel.HeatPerSecond),
                    state.RemainingAddedFuel.Item.Id,
                    state.RemainingAddedFuel.Item.Count,
                    [.. state.RemainingAddedFuel.Item.Instances.Select(item => item.InstanceId)]
                );
            }
            else
            {
                fuel = null;
            }

            var burning = new Types.Workshop.SmeltingSlot.BurningR(
                !state.Completed ? TimeFormatter.FormatTime(state.BurnStartTime) : null,
                !state.Completed ? TimeFormatter.FormatTime(state.BurnEndTime) : null,
                TimeFormatter.FormatDuration(state.RemainingHeat * 1000 / state.CurrentBurningFuel.HeatPerSecond),
                (float)state.CurrentBurningFuel.BurnDuration * state.CurrentBurningFuel.HeatPerSecond - state.RemainingHeat,
                new Types.Workshop.SmeltingSlot.FuelR(
                    new BurnRate(state.CurrentBurningFuel.BurnDuration, state.CurrentBurningFuel.HeatPerSecond),
                    state.CurrentBurningFuel.Item.Id,
                    state.CurrentBurningFuel.Item.Count,
                    [.. state.CurrentBurningFuel.Item.Instances.Select(item => item.InstanceId)]
                )
            );

            return new Types.Workshop.SmeltingSlot(
                fuel,
                burning,
                activeJob.SessionId,
                activeJob.RecipeId,
                new OutputItem(state.Output.Id, state.Output.Count),
                state.Input.Count > 0 ? [new Types.Workshop.InputItem(state.Input.Id, state.Input.Count, state.Input.Instances.Select(item => item.InstanceId).ToArray())] : [],
                state.CompletedRounds,
                state.AvailableRounds,
                state.TotalRounds,
                !state.Completed ? TimeFormatter.FormatTime(state.NextCompletionTime) : null,
                !state.Completed ? TimeFormatter.FormatTime(state.TotalCompletionTime) : null,
                state.Completed ? State.COMPLETED : State.ACTIVE,
                null,
                null,
                streamVersion
            );
        }
        else
        {
            SmeltingSlot.BurningR? burningModel = smeltingSlotModel.Burning;
            Types.Workshop.SmeltingSlot.BurningR? burning = burningModel is not null ? new Types.Workshop.SmeltingSlot.BurningR(
                null,
                null,
                TimeFormatter.FormatDuration(burningModel.RemainingHeat * 1000 / burningModel.Fuel.HeatPerSecond),
                (float)burningModel.Fuel.BurnDuration * burningModel.Fuel.HeatPerSecond * burningModel.Fuel.Item.Count - burningModel.RemainingHeat,
                new Types.Workshop.SmeltingSlot.FuelR(
                    new BurnRate(burningModel.Fuel.BurnDuration, burningModel.Fuel.HeatPerSecond),
                    burningModel.Fuel.Item.Id,
                    burningModel.Fuel.Item.Count,
                    [.. burningModel.Fuel.Item.Instances.Select(item => item.InstanceId)]
                )
            ) : null;
            return new Types.Workshop.SmeltingSlot(null, burning, null, null, null, null, 0, 0, 0, null, null, State.EMPTY, null, null, streamVersion);
        }
    }

    private sealed record StartRequestCrafting(
        string SessionId,
        string RecipeId,
        int Multiplier,
        StartRequestCrafting.Item[] Ingredients
    )
    {
        public sealed record Item(
            string ItemId,
            int Quantity,
            string[] ItemInstanceIds
        );
    }

    private sealed record StartRequestSmelting(
        string SessionId,
        string RecipeId,
        int Multiplier,
        StartRequestSmelting.Item Input,
        StartRequestSmelting.Item Fuel
    )
    {
        public sealed record Item(
            string ItemId,
            int Quantity,
            string[] ItemInstanceIds
        );
    }
}
