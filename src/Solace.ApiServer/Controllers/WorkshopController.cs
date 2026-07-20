using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Primitives;
using System.Diagnostics;
using Solace.ApiServer.Utils;
using Solace.Common.Exceptions;
using Solace.Common.Utils;
using Solace.StaticData;
using BurnRate = Solace.ApiServer.Types.Common.BurnRate;
using CraftingCalculator = Solace.ApiServer.Utils.CraftingCalculator;
using CraftingSlot = Solace.DB.Models.Player.Workshop.CraftingSlotEF;
using EarthApiResponse = Solace.ApiServer.Utils.EarthApiResponse;
using ExpectedPurchasePriceR = Solace.ApiServer.Types.Common.ExpectedPurchasePriceR;
using FinishPrice = Solace.ApiServer.Types.Workshop.FinishPrice;
using InputItem = Solace.DB.Models.Player.Workshop.InputItem;
using OutputItem = Solace.ApiServer.Types.Workshop.OutputItem;
using Rewards = Solace.ApiServer.Utils.Rewards;
using SmeltingCalculator = Solace.ApiServer.Utils.SmeltingCalculator;
using SmeltingSlotEF = Solace.DB.Models.Player.Workshop.SmeltingSlotEF;
using SplitRubies = Solace.ApiServer.Types.Profile.SplitRubies;
using State = Solace.ApiServer.Types.Workshop.State;
using TimeFormatter = Solace.ApiServer.Utils.TimeFormatter;
using UnlockPrice = Solace.ApiServer.Types.Workshop.UnlockPrice;
using Solace.DB;
using Microsoft.EntityFrameworkCore;
using Solace.DB.Models.Player;
using Solace.DB.Models.Common;

namespace Solace.ApiServer.Controllers;

[Authorize]
[ApiVersion("1.1")]
[Route("1/api/v{version:apiVersion}")]
internal sealed class WorkshopController : SolaceControllerBase
{
    private readonly EarthDbContext _earthDb;
    private readonly StaticData.StaticDataProvider _staticData;

    public WorkshopController(EarthDbContext earthDb, StaticData.StaticDataProvider staticData)
    {
        _earthDb = earthDb;
        _staticData = staticData;
    }

    [HttpGet("player/utilityBlocks")]
    public async Task<Results<ContentHttpResult, BadRequest>> GetUtilityBlocks(CancellationToken cancellationToken)
    {
        if (!TryGetAccountId(out var accountId))
        {
            return TypedResults.BadRequest();
        }

        // request.timestamp
        var requestStartedOn = HttpContext.GetTimestamp();

        var craftingSlots = await _earthDb.CraftingSlots
            .AsNoTracking()
            .FirstAsync(craftingSlots => craftingSlots.Id == accountId, cancellationToken: cancellationToken);

        var smeltingSlots = await _earthDb.SmeltingSlots
            .AsNoTracking()
            .FirstAsync(smeltingSlots => smeltingSlots.Id == accountId, cancellationToken: cancellationToken);

        var versions = await _earthDb.AccountVersions
            .AsNoTracking()
            .Select(versions => new { versions.Id, versions.Crafting, versions.Smelting, })
            .FirstAsync(versions => versions.Id == accountId, cancellationToken: cancellationToken);

        Dictionary<string, object> workshop = new(StringComparer.Ordinal)
        {
            ["crafting"] = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["1"] = CraftingSlotModelToResponseIncludingLocked(craftingSlots.Slots[0], requestStartedOn, versions.Crafting, 1),
                ["2"] = CraftingSlotModelToResponseIncludingLocked(craftingSlots.Slots[1], requestStartedOn, versions.Crafting, 2),
                ["3"] = CraftingSlotModelToResponseIncludingLocked(craftingSlots.Slots[2], requestStartedOn, versions.Crafting, 3),
            },
            ["smelting"] = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["1"] = SmeltingSlotModelToResponseIncludingLocked(smeltingSlots.Slots[0], requestStartedOn, versions.Smelting, 1),
                ["2"] = SmeltingSlotModelToResponseIncludingLocked(smeltingSlots.Slots[1], requestStartedOn, versions.Smelting, 2),
                ["3"] = SmeltingSlotModelToResponseIncludingLocked(smeltingSlots.Slots[2], requestStartedOn, versions.Smelting, 3),
            },
        };

        return EarthJson(workshop);
    }

    [HttpGet("crafting/{slotIndex}")]
    public async Task<Results<ContentHttpResult, BadRequest>> GetCraftingStatus(int slotIndex, CancellationToken cancellationToken)
    {
        if (!TryGetAccountId(out var accountId) || slotIndex is < 1 or > 3)
        {
            return TypedResults.BadRequest();
        }

        // request.timestamp
        var requestStartedOn = HttpContext.GetTimestamp();

        var craftingSlots = await _earthDb.CraftingSlots
            .AsNoTracking()
            .FirstAsync(craftingSlots => craftingSlots.Id == accountId, cancellationToken: cancellationToken);

        var versions = await _earthDb.AccountVersions
            .AsNoTracking()
            .Select(versions => new { versions.Id, versions.Crafting, })
            .FirstAsync(versions => versions.Id == accountId, cancellationToken: cancellationToken);

        return EarthJson(CraftingSlotModelToResponseIncludingLocked(craftingSlots.Slots[slotIndex - 1], requestStartedOn, versions.Crafting, slotIndex));
    }

    [HttpGet("smelting/{slotIndex}")]
    public async Task<Results<ContentHttpResult, BadRequest>> GetSmeltingStatus(int slotIndex, CancellationToken cancellationToken)
    {
        if (!TryGetAccountId(out var accountId) || slotIndex is < 1 or > 3)
        {
            return TypedResults.BadRequest();
        }

        // request.timestamp
        var requestStartedOn = HttpContext.GetTimestamp();

        var smeltingSlots = await _earthDb.SmeltingSlots
            .AsNoTracking()
            .FirstAsync(smeltingSlots => smeltingSlots.Id == accountId, cancellationToken: cancellationToken);

        var versions = await _earthDb.AccountVersions
            .AsNoTracking()
            .Select(versions => new { versions.Id, versions.Smelting, })
            .FirstAsync(versions => versions.Id == accountId, cancellationToken: cancellationToken);

        return EarthJson(SmeltingSlotModelToResponseIncludingLocked(smeltingSlots.Slots[slotIndex - 1], requestStartedOn, versions.Smelting, slotIndex));
    }

    [HttpPost("crafting/{slotIndex}/start")]
    public async Task<Results<ContentHttpResult, BadRequest>> StartCrafting(int slotIndex, CancellationToken cancellationToken)
    {
        if (!TryGetAccountId(out var accountId) || slotIndex is < 1 or > 3)
        {
            return TypedResults.BadRequest();
        }

        // request.timestamp
        var requestStartedOn = HttpContext.GetTimestamp();

        StartRequestCrafting? startRequest = await Request.Body.AsJsonAsync(AppJsonContext.Default.StartRequestCrafting, cancellationToken);
        if (startRequest is null || startRequest.Multiplier < 1)
        {
            return TypedResults.BadRequest();
        }

        if (startRequest.Ingredients.Any(item => item is null || item.Quantity < 1 || item.ItemInstanceIds is not null && item.ItemInstanceIds.Length > 0 && item.ItemInstanceIds.Length != item.Quantity))
        {
            return TypedResults.BadRequest();
        }

        Catalog.RecipesCatalogR.CraftingRecipe? recipe = _staticData.Catalog.RecipesCatalog.GetCraftingRecipe(startRequest.RecipeId);

        if (recipe is null)
        {
            return TypedResults.BadRequest();
        }

        if (recipe.ReturnItems.Length > 0)
        {
            throw new UnsupportedOperationException(); // TODO: implement returnItems
        }

        var craftingSlots = await _earthDb.CraftingSlots
            .AsTracking()
            .FirstAsync(craftingSlots => craftingSlots.Id == accountId, cancellationToken: cancellationToken);

        var hotbar = await _earthDb.Hotbars
            .AsTracking()
            .FirstAsync(hotbar => hotbar.Id == accountId, cancellationToken: cancellationToken);

        var craftingSlot = craftingSlots.Slots[slotIndex - 1];

        if (craftingSlot.Locked || craftingSlot.ActiveJob is not null)
        {
            return EarthJson(new Dictionary<string, object>(StringComparer.Ordinal), new EarthApiResponse.UpdatesResponse());
        }

        var results = new ResultsEF.Builder();

        var providedItems = new InputItem[startRequest.Ingredients.Length];
        for (var index = 0; index < startRequest.Ingredients.Length; index++)
        {
            StartRequestCraftingItem item = startRequest.Ingredients[index];
            if (item.ItemInstanceIds is null || item.ItemInstanceIds.Length == 0)
            {
                if (!await InventoryUtils.TakeStackableItemsAsync(_earthDb, results, accountId, item.ItemId, item.Quantity, cancellationToken))
                {
                    return EarthJson(new Dictionary<string, object>(StringComparer.Ordinal), new EarthApiResponse.UpdatesResponse());
                }

                providedItems[index] = new InputItem(item.ItemId, item.Quantity, []);
            }
            else
            {
                var instances = await InventoryUtils.TakeInstanceItemsAsync(_earthDb, results, accountId, item.ItemId, item.ItemInstanceIds, cancellationToken);
                if (instances is null)
                {
                    return EarthJson(new Dictionary<string, object>(StringComparer.Ordinal), new EarthApiResponse.UpdatesResponse());
                }

                providedItems[index] = new InputItem(item.ItemId, item.Quantity, [.. instances.Select(instance => new NonStackableItemInstance(instance.InstanceId, instance.Wear))]);
            }
        }

        await HotbarUtils.LimitToInventoryAsync(_earthDb, accountId, hotbar, cancellationToken);

        var inputItems = new List<List<InputItem>>(recipe.Ingredients.Length);
        foreach (Catalog.RecipesCatalogR.CraftingRecipe.Ingredient ingredient in recipe.Ingredients)
        {
            var ingredientItems = new List<InputItem>(providedItems.Length);
            var requiredCount = ingredient.Count * startRequest.Multiplier;
            for (var index = 0; index < providedItems.Length; index++)
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
                    ingredientItems.Add(providedItem);
                    providedItems[index] = new InputItem(providedItem.Id, 0, []);
                }
                else
                {
                    NonStackableItemInstance[] takenInstances;
                    NonStackableItemInstance[] remainingInstances;
                    if (providedItem.Instances.Length > 0)
                    {
                        takenInstances = providedItem.Instances[..requiredCount];
                        remainingInstances = providedItem.Instances[requiredCount..];
                    }
                    else
                    {
                        takenInstances = [];
                        remainingInstances = [];
                    }

                    ingredientItems.Add(new InputItem(providedItem.Id, requiredCount, takenInstances));
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
                return EarthJson(new Dictionary<string, object>(StringComparer.Ordinal), new EarthApiResponse.UpdatesResponse());
            }

            if (ingredientItems.Count == 0)
            {
                throw new UnreachableException();
            }

            inputItems.Add(ingredientItems);
        }

        if (inputItems.Count != recipe.Ingredients.Length)
        {
            throw new UnreachableException();
        }

        if (providedItems.Any(item => item.Count > 0))
        {
            return EarthJson(new Dictionary<string, object>(StringComparer.Ordinal), new EarthApiResponse.UpdatesResponse());
        }

        craftingSlot.ActiveJob = new CraftingSlot.ActiveCraftingJob(startRequest.SessionId, recipe.Id, requestStartedOn, [.. inputItems.Select(inputItems1 => new CraftingSlot.InputRow([.. inputItems1]))], startRequest.Multiplier, 0, false);

        await _earthDb.SaveChangesAsync(cancellationToken);

        results
            .Crafting()
            .Inventory();

        return EarthJson(new Dictionary<string, object>(StringComparer.Ordinal), new EarthApiResponse.UpdatesResponse(await results.BuildAsync(_earthDb, accountId, cancellationToken)));
    }

    [HttpPost("smelting/{slotIndex}/start")]
    public async Task<Results<ContentHttpResult, BadRequest>> StartSmelting(int slotIndex, CancellationToken cancellationToken)
    {
        if (!TryGetAccountId(out var accountId) || slotIndex is < 1 or > 3)
        {
            return TypedResults.BadRequest();
        }

        // request.timestamp
        var requestStartedOn = HttpContext.GetTimestamp();

        StartRequestSmelting? startRequest = await Request.Body.AsJsonAsync(AppJsonContext.Default.StartRequestSmelting, cancellationToken);
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

        Catalog.RecipesCatalogR.SmeltingRecipe? recipe = _staticData.Catalog.RecipesCatalog.GetSmeltingRecipe(startRequest.RecipeId);
        Catalog.ItemsCatalogR.Item? fuelCatalogItem = startRequest.Fuel is not null ? _staticData.Catalog.ItemsCatalog.GetItem(startRequest.Fuel.ItemId) : null;
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

        var smeltingSlots = await _earthDb.SmeltingSlots
            .AsTracking()
            .FirstAsync(smeltingSlots => smeltingSlots.Id == accountId, cancellationToken: cancellationToken);

        var hotbar = await _earthDb.Hotbars
            .AsTracking()
            .FirstAsync(hotbar => hotbar.Id == accountId, cancellationToken: cancellationToken);

        var smeltingSlot = smeltingSlots.Slots[slotIndex - 1];

        if (smeltingSlot.Locked || smeltingSlot.ActiveJob is not null)
        {
            return EarthJson(new Dictionary<string, object>(StringComparer.Ordinal), new EarthApiResponse.UpdatesResponse());
        }

        var results = new ResultsEF.Builder();

        InputItem input;
        if (startRequest.Input.ItemInstanceIds is null or [])
        {
            if (!await InventoryUtils.TakeStackableItemsAsync(_earthDb, results, accountId, startRequest.Input.ItemId, startRequest.Input.Quantity, cancellationToken))
            {
                return EarthJson(new Dictionary<string, object>(StringComparer.Ordinal), new EarthApiResponse.UpdatesResponse());
            }

            input = new InputItem(startRequest.Input.ItemId, startRequest.Input.Quantity, []);
        }
        else
        {
            var instances = await InventoryUtils.TakeInstanceItemsAsync(_earthDb, results, accountId, startRequest.Input.ItemId, startRequest.Input.ItemInstanceIds, cancellationToken);
            if (instances is null)
            {
                return EarthJson(new Dictionary<string, object>(StringComparer.Ordinal), new EarthApiResponse.UpdatesResponse());
            }

            input = new InputItem(startRequest.Input.ItemId, startRequest.Input.Quantity, [.. instances.Select(instance => new NonStackableItemInstance(instance.InstanceId, instance.Wear))]);
        }

        SmeltingSlotEF.Fuel? fuel;
        var requiredFuelHeat = recipe.HeatRequired * startRequest.Multiplier - (smeltingSlot.Burning is not null ? smeltingSlot.Burning.RemainingHeat : 0);
        if (startRequest.Fuel is not null && startRequest.Fuel.Quantity > 0)
        {
            var requiredFuelCount = 0;
            while (requiredFuelHeat > 0)
            {
                requiredFuelCount += 1;
                requiredFuelHeat -= fuelCatalogItem.FuelInfo.HeatPerSecond * fuelCatalogItem.FuelInfo.BurnTime;
            }

            if (startRequest.Fuel.Quantity < requiredFuelCount)
            {
                return EarthJson(new Dictionary<string, object>(StringComparer.Ordinal), new EarthApiResponse.UpdatesResponse());
            }

            if (requiredFuelCount > 0)
            {
                InputItem fuelItem;
                if (startRequest.Fuel.ItemInstanceIds is null or [])
                {
                    if (!await InventoryUtils.TakeStackableItemsAsync(_earthDb, results, accountId, startRequest.Fuel.ItemId, requiredFuelCount, cancellationToken))
                    {
                        return EarthJson(new Dictionary<string, object>(StringComparer.Ordinal), new EarthApiResponse.UpdatesResponse());
                    }

                    fuelItem = new InputItem(startRequest.Fuel.ItemId, requiredFuelCount, []);
                }
                else
                {
                    var instances = await InventoryUtils.TakeInstanceItemsAsync(_earthDb, results, accountId, startRequest.Fuel.ItemId, startRequest.Fuel.ItemInstanceIds.Take(requiredFuelCount), cancellationToken);
                    if (instances is null)
                    {
                        return EarthJson(new Dictionary<string, object>(StringComparer.Ordinal), new EarthApiResponse.UpdatesResponse());
                    }

                    fuelItem = new InputItem(startRequest.Fuel.ItemId, requiredFuelCount, [.. instances.Select(instance => new NonStackableItemInstance(instance.InstanceId, instance.Wear))]);
                }

                fuel = new SmeltingSlotEF.Fuel(fuelItem, TimeSpan.FromSeconds(fuelCatalogItem.FuelInfo.BurnTime), fuelCatalogItem.FuelInfo.HeatPerSecond);
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
                return EarthJson(new Dictionary<string, object>(StringComparer.Ordinal), new EarthApiResponse.UpdatesResponse());
            }

            fuel = null;
        }

        await HotbarUtils.LimitToInventoryAsync(_earthDb, accountId, hotbar, cancellationToken);

        smeltingSlot.ActiveJob = new SmeltingSlotEF.ActiveSmeltingJob(startRequest.SessionId, recipe.Id, requestStartedOn, input, fuel, startRequest.Multiplier, 0, false);

        await _earthDb.SaveChangesAsync(cancellationToken);

        results
            .Smelting()
            .Inventory();

        return EarthJson(new Dictionary<string, object>(StringComparer.Ordinal), new EarthApiResponse.UpdatesResponse(await results.BuildAsync(_earthDb, accountId, cancellationToken)));
    }

    [HttpPost("crafting/{slotIndex}/collectItems")]
    public async Task<Results<ContentHttpResult, BadRequest>> CollectCraftingItems(int slotIndex, CancellationToken cancellationToken)
    {
        if (!TryGetAccountId(out var accountId) || slotIndex is < 1 or > 3)
        {
            return TypedResults.BadRequest();
        }

        // request.timestamp
        var requestStartedOn = HttpContext.GetTimestamp();

        var craftingSlots = await _earthDb.CraftingSlots
            .AsTracking()
            .FirstAsync(craftingSlots => craftingSlots.Id == accountId, cancellationToken: cancellationToken);

        var craftingSlot = craftingSlots.Slots[slotIndex - 1];

        var rewards = new Rewards();
        if (craftingSlot.ActiveJob is not null)
        {
            CraftingCalculator.State state = CraftingCalculator.CalculateState(requestStartedOn, craftingSlot.ActiveJob, _staticData.Catalog);

            var quantity = state.AvailableRounds * state.Output.Count;
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
                CraftingSlot.ActiveCraftingJob activeJob = craftingSlot.ActiveJob;
                craftingSlot.ActiveJob = new CraftingSlot.ActiveCraftingJob(activeJob.SessionId, activeJob.RecipeId, activeJob.StartTime, activeJob.Input, activeJob.TotalRounds, activeJob.CollectedRounds + state.AvailableRounds, activeJob.FinishedEarly);
            }
        }

        await _earthDb.SaveChangesAsync(cancellationToken);

        var results = new ResultsEF.Builder()
            .Crafting();

        await ActivityLogUtils.AddEntryAsync(_earthDb, results, accountId, new DB.Models.Player.CraftingCompletedEntryEF(accountId, requestStartedOn, rewards.ToDBRewardsModel()), cancellationToken);
        await rewards.ToRedeemQueryAsync(_earthDb, results, accountId, requestStartedOn, _staticData, cancellationToken);

        return EarthJson(new Dictionary<string, object>(StringComparer.Ordinal)
            {
                { "rewards", rewards.ToApiResponse() }
            }, new EarthApiResponse.UpdatesResponse(await results.BuildAsync(_earthDb, accountId, cancellationToken)));
    }

    [HttpPost("smelting/{slotIndex}/collectItems")]
    public async Task<Results<ContentHttpResult, BadRequest>> CollectSmeltingItems(int slotIndex, CancellationToken cancellationToken)
    {
        if (!TryGetAccountId(out var accountId) || slotIndex is < 1 or > 3)
        {
            return TypedResults.BadRequest();
        }

        // request.timestamp
        var requestStartedOn = HttpContext.GetTimestamp();

        var smeltingSlots = await _earthDb.SmeltingSlots
            .AsTracking()
            .FirstAsync(smeltingSlots => smeltingSlots.Id == accountId, cancellationToken: cancellationToken);

        var smeltingSlot = smeltingSlots.Slots[slotIndex - 1];

        var rewards = new Rewards();
        if (smeltingSlot.ActiveJob is not null)
        {
            SmeltingCalculator.State state = SmeltingCalculator.CalculateState(requestStartedOn, smeltingSlot.ActiveJob, smeltingSlot.Burning, _staticData.Catalog);

            var quantity = state.AvailableRounds * state.Output.Count;
            if (quantity > 0)
            {
                rewards.AddItem(state.Output.Id, quantity);
            }

            if (state.Completed)
            {
                smeltingSlot.ActiveJob = null;
                if (state.RemainingHeat > 0)
                {
                    smeltingSlot.Burning = new SmeltingSlotEF.BurningR(
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
                SmeltingSlotEF.ActiveSmeltingJob activeJob = smeltingSlot.ActiveJob;
                smeltingSlot.ActiveJob = new SmeltingSlotEF.ActiveSmeltingJob(activeJob.SessionId, activeJob.RecipeId, activeJob.StartTime, activeJob.Input, activeJob.AddedFuel, activeJob.TotalRounds, activeJob.CollectedRounds + state.AvailableRounds, activeJob.FinishedEarly);
            }
        }

        await _earthDb.SaveChangesAsync(cancellationToken);

        var results = new ResultsEF.Builder()
            .Smelting();

        await ActivityLogUtils.AddEntryAsync(_earthDb, results, accountId, new SmeltingCompletedEntryEF(accountId, requestStartedOn, rewards.ToDBRewardsModel()), cancellationToken);
        await rewards.ToRedeemQueryAsync(_earthDb, results, accountId, requestStartedOn, _staticData, cancellationToken);

        return EarthJson(new Dictionary<string, object>(StringComparer.Ordinal)
            {
                { "rewards", rewards.ToApiResponse() }
            }, new EarthApiResponse.UpdatesResponse(await results.BuildAsync(_earthDb, accountId, cancellationToken)));
    }

    [HttpPost("crafting/{slotIndex}/stop")]
    public async Task<Results<ContentHttpResult, BadRequest>> StopCraftingJob(int slotIndex, CancellationToken cancellationToken)
    {
        if (!TryGetAccountId(out var accountId) || slotIndex is < 1 or > 3)
        {
            return TypedResults.BadRequest();
        }

        // request.timestamp
        var requestStartedOn = HttpContext.GetTimestamp();

        var craftingSlots = await _earthDb.CraftingSlots
            .AsTracking()
            .FirstAsync(craftingSlots => craftingSlots.Id == accountId, cancellationToken: cancellationToken);

        var craftingSlot = craftingSlots.Slots[slotIndex - 1];

        if (craftingSlot.ActiveJob is null)
        {
            var versions = await _earthDb.AccountVersions
                .AsNoTracking()
                .Select(versions => new { versions.Id, versions.Crafting, })
                .FirstAsync(versions => versions.Id == accountId, cancellationToken: cancellationToken);

            return EarthJson(CraftingSlotModelToResponse(craftingSlot, requestStartedOn, versions.Crafting));
        }

        var state = CraftingCalculator.CalculateState(requestStartedOn, craftingSlot.ActiveJob, _staticData.Catalog);

        var results = new ResultsEF.Builder();

        foreach (var inputItem in state.Input)
        {
            if (inputItem.Instances.Length > 0)
            {
                await InventoryUtils.AddInstanceItemsAsync(_earthDb, results, inputItem.Instances.Select(instance => new NonStackableItemInstanceEF(accountId, inputItem.Id, instance.InstanceId, instance.Wear)), cancellationToken);
            }
            else if (inputItem.Count > 0)
            {
                await InventoryUtils.AddStackableItemsAsync(_earthDb, results, accountId, inputItem.Id, inputItem.Count, cancellationToken);
            }

            await JournalUtils.AddCollectedItemAsync(_earthDb, results, accountId, inputItem.Id, requestStartedOn, 0, cancellationToken);
        }

        var rewards = new Rewards();
        var outputQuantity = state.AvailableRounds * state.Output.Count;
        if (outputQuantity > 0)
        {
            rewards.AddItem(state.Output.Id, outputQuantity);
        }

        craftingSlot.ActiveJob = null;

        await _earthDb.SaveChangesAsync(cancellationToken);

        results
            .Crafting();

        await ActivityLogUtils.AddEntryAsync(_earthDb, results, accountId, new CraftingCompletedEntryEF(accountId, requestStartedOn, rewards.ToDBRewardsModel()), cancellationToken);
        await rewards.ToRedeemQueryAsync(_earthDb, results, accountId, requestStartedOn, _staticData, cancellationToken);

        var buildResults = await results.BuildAsync(_earthDb, accountId, cancellationToken);

        return EarthJson(CraftingSlotModelToResponse(craftingSlot, requestStartedOn, buildResults.Crafting!.Value), new EarthApiResponse.UpdatesResponse(buildResults));
    }

    [HttpPost("smelting/{slotIndex}/stop")]
    public async Task<Results<ContentHttpResult, BadRequest>> StopSmeltingJob(int slotIndex, CancellationToken cancellationToken)
    {
        if (!TryGetAccountId(out var accountId) || slotIndex is < 1 or > 3)
        {
            return TypedResults.BadRequest();
        }

        // request.timestamp
        var requestStartedOn = HttpContext.GetTimestamp();

        var smeltingSlots = await _earthDb.SmeltingSlots
            .AsTracking()
            .FirstAsync(smeltingSlots => smeltingSlots.Id == accountId, cancellationToken: cancellationToken);

        var smeltingSlot = smeltingSlots.Slots[slotIndex - 1];

        if (smeltingSlot.ActiveJob is null)
        {
            var versions = await _earthDb.AccountVersions
                .AsNoTracking()
                .Select(versions => new { versions.Id, versions.Smelting, })
                .FirstAsync(versions => versions.Id == accountId, cancellationToken: cancellationToken);

            return EarthJson(SmeltingSlotModelToResponse(smeltingSlot, requestStartedOn, versions.Smelting), new EarthApiResponse.UpdatesResponse());
        }

        SmeltingCalculator.State state = SmeltingCalculator.CalculateState(requestStartedOn, smeltingSlot.ActiveJob, smeltingSlot.Burning, _staticData.Catalog);

        var results = new ResultsEF.Builder();

        if (state.Input.Instances.Length > 0)
        {
            await InventoryUtils.AddInstanceItemsAsync(_earthDb, results, state.Input.Instances.Select(instance => new NonStackableItemInstanceEF(accountId, state.Input.Id, instance.InstanceId, instance.Wear)), cancellationToken);
        }
        else if (state.Input.Count > 0)
        {
            await InventoryUtils.AddStackableItemsAsync(_earthDb, results, accountId, state.Input.Id, state.Input.Count, cancellationToken);
        }

        await JournalUtils.AddCollectedItemAsync(_earthDb, results, accountId, state.Input.Id, requestStartedOn, 0, cancellationToken);

        if (state.RemainingAddedFuel is not null)
        {
            if (state.RemainingAddedFuel.Item.Instances.Length > 0)
            {
                await InventoryUtils.AddInstanceItemsAsync(_earthDb, results, state.RemainingAddedFuel.Item.Instances.Select(instance => new NonStackableItemInstanceEF(accountId, state.RemainingAddedFuel.Item.Id, instance.InstanceId, instance.Wear)), cancellationToken);
            }
            else if (state.RemainingAddedFuel.Item.Count > 0)
            {
                await InventoryUtils.AddStackableItemsAsync(_earthDb, results, accountId, state.RemainingAddedFuel.Item.Id, state.RemainingAddedFuel.Item.Count, cancellationToken);
            }

            await JournalUtils.AddCollectedItemAsync(_earthDb, results, accountId, state.RemainingAddedFuel.Item.Id, requestStartedOn, 0, cancellationToken);
        }

        var rewards = new Rewards();
        var outputQuantity = state.AvailableRounds * state.Output.Count;
        if (outputQuantity > 0)
        {
            rewards.AddItem(state.Output.Id, outputQuantity);
        }

        smeltingSlot.ActiveJob = null;
        if (state.RemainingHeat > 0)
        {
            smeltingSlot.Burning = new SmeltingSlotEF.BurningR(state.CurrentBurningFuel, state.RemainingHeat);
        }
        else
        {
            smeltingSlot.Burning = null;
        }

        await _earthDb.SaveChangesAsync(cancellationToken);

        results
            .Smelting();

        await ActivityLogUtils.AddEntryAsync(_earthDb, results, accountId, new SmeltingCompletedEntryEF(accountId, requestStartedOn, rewards.ToDBRewardsModel()), cancellationToken);
        await rewards.ToRedeemQueryAsync(_earthDb, results, accountId, requestStartedOn, _staticData, cancellationToken);

        var buildResults = await results.BuildAsync(_earthDb, accountId, cancellationToken);

        return EarthJson(SmeltingSlotModelToResponse(smeltingSlot, requestStartedOn, buildResults.Smelting!.Value), new EarthApiResponse.UpdatesResponse(buildResults));
    }

    [HttpPost("crafting/{slotIndex}/finish")]
    public async Task<Results<ContentHttpResult, BadRequest>> FinishCrafting(int slotIndex, CancellationToken cancellationToken)
    {
        if (!TryGetAccountId(out var accountId) || slotIndex is < 1 or > 3)
        {
            return TypedResults.BadRequest();
        }

        // request.timestamp
        var requestStartedOn = HttpContext.GetTimestamp();

        ExpectedPurchasePriceR? expectedPurchasePrice = await Request.Body.AsJsonAsync(AppJsonContext.Default.ExpectedPurchasePriceR, cancellationToken);
        if (expectedPurchasePrice is null || expectedPurchasePrice.ExpectedPurchasePrice < 0)
        {
            return TypedResults.BadRequest();
        }

        var craftingSlots = await _earthDb.CraftingSlots
            .AsTracking()
            .FirstAsync(craftingSlots => craftingSlots.Id == accountId, cancellationToken: cancellationToken);

        var profile = await _earthDb.Profiles
            .AsTracking()
            .FirstAsync(profile => profile.Id == accountId, cancellationToken: cancellationToken);

        var craftingSlot = craftingSlots.Slots[slotIndex - 1];

        if (craftingSlot.ActiveJob is null)
        {
            return EarthJson(new SplitRubies(profile.Rubies.Purchased, profile.Rubies.Earned), new EarthApiResponse.UpdatesResponse());
        }

        CraftingCalculator.State state = CraftingCalculator.CalculateState(requestStartedOn, craftingSlot.ActiveJob, _staticData.Catalog);
        if (state.Completed)
        {
            return EarthJson(new SplitRubies(profile.Rubies.Purchased, profile.Rubies.Earned), new EarthApiResponse.UpdatesResponse());
        }

        var remainingTime = state.TotalCompletionTime - requestStartedOn;
        if (remainingTime < TimeSpan.Zero)
        {
            return EarthJson(new SplitRubies(profile.Rubies.Purchased, profile.Rubies.Earned), new EarthApiResponse.UpdatesResponse());
        }

        var finishPrice = CraftingCalculator.CalculateFinishPrice(remainingTime);

        if (expectedPurchasePrice.ExpectedPurchasePrice < finishPrice.Price)
        {
            return EarthJson(new SplitRubies(profile.Rubies.Purchased, profile.Rubies.Earned), new EarthApiResponse.UpdatesResponse());
        }

        if (!profile.Rubies.Spend(finishPrice.Price))
        {
            return EarthJson(new SplitRubies(profile.Rubies.Purchased, profile.Rubies.Earned), new EarthApiResponse.UpdatesResponse());
        }

        CraftingSlot.ActiveCraftingJob activeJob = craftingSlot.ActiveJob;
        craftingSlot.ActiveJob = new CraftingSlot.ActiveCraftingJob(activeJob.SessionId, activeJob.RecipeId, activeJob.StartTime, activeJob.Input, activeJob.TotalRounds, activeJob.CollectedRounds, true);

        await _earthDb.SaveChangesAsync(cancellationToken);

        var results = new ResultsEF.Builder()
            .Crafting()
            .Profile();

        return EarthJson(new SplitRubies(profile.Rubies.Purchased, profile.Rubies.Earned), new EarthApiResponse.UpdatesResponse(await results.BuildAsync(_earthDb, accountId, cancellationToken)));
    }

    [HttpPost("smelting/{slotIndex}/finish")]
    public async Task<Results<ContentHttpResult, BadRequest>> FinishSmelting(int slotIndex, CancellationToken cancellationToken)
    {
        if (!TryGetAccountId(out var accountId) || slotIndex is < 1 or > 3)
        {
            return TypedResults.BadRequest();
        }

        // request.timestamp
        var requestStartedOn = HttpContext.GetTimestamp();

        ExpectedPurchasePriceR? expectedPurchasePrice = await Request.Body.AsJsonAsync(AppJsonContext.Default.ExpectedPurchasePriceR, cancellationToken);
        if (expectedPurchasePrice is null || expectedPurchasePrice.ExpectedPurchasePrice < 0)
        {
            return TypedResults.BadRequest();
        }

        var smeltingSlots = await _earthDb.SmeltingSlots
                .AsTracking()
                .FirstAsync(smeltingSlots => smeltingSlots.Id == accountId, cancellationToken: cancellationToken);

        var profile = await _earthDb.Profiles
            .AsTracking()
            .FirstAsync(profile => profile.Id == accountId, cancellationToken: cancellationToken);

        var smeltingSlot = smeltingSlots.Slots[slotIndex - 1];

        if (smeltingSlot.ActiveJob is null)
        {
            return EarthJson(new SplitRubies(profile.Rubies.Purchased, profile.Rubies.Earned), new EarthApiResponse.UpdatesResponse());
        }

        SmeltingCalculator.State state = SmeltingCalculator.CalculateState(requestStartedOn, smeltingSlot.ActiveJob, smeltingSlot.Burning, _staticData.Catalog);
        if (state.Completed)
        {
            return EarthJson(new SplitRubies(profile.Rubies.Purchased, profile.Rubies.Earned), new EarthApiResponse.UpdatesResponse());
        }

        var remainingTime = state.TotalCompletionTime - requestStartedOn;
        if (remainingTime < TimeSpan.Zero)
        {
            return EarthJson(new SplitRubies(profile.Rubies.Purchased, profile.Rubies.Earned), new EarthApiResponse.UpdatesResponse());
        }

        var finishPrice = SmeltingCalculator.CalculateFinishPrice(remainingTime);

        if (expectedPurchasePrice.ExpectedPurchasePrice < finishPrice.Price)
        {
            return EarthJson(new SplitRubies(profile.Rubies.Purchased, profile.Rubies.Earned), new EarthApiResponse.UpdatesResponse());
        }

        if (!profile.Rubies.Spend(finishPrice.Price))
        {
            return EarthJson(new SplitRubies(profile.Rubies.Purchased, profile.Rubies.Earned), new EarthApiResponse.UpdatesResponse());
        }

        SmeltingSlotEF.ActiveSmeltingJob activeJob = smeltingSlot.ActiveJob;
        smeltingSlot.ActiveJob = new SmeltingSlotEF.ActiveSmeltingJob(activeJob.SessionId, activeJob.RecipeId, activeJob.StartTime, activeJob.Input, activeJob.AddedFuel, activeJob.TotalRounds, activeJob.CollectedRounds, true);

        await _earthDb.SaveChangesAsync(cancellationToken);

        var results = new ResultsEF.Builder()
            .Smelting()
            .Profile();

        return EarthJson(new SplitRubies(profile.Rubies.Purchased, profile.Rubies.Earned), new EarthApiResponse.UpdatesResponse(await results.BuildAsync(_earthDb, accountId, cancellationToken)));
    }

    [HttpGet("crafting/finish/price")]
    public Results<ContentHttpResult, BadRequest> GetCraftingPrice()
    {
        if (!Request.Query.TryGetValue("remainingTime", out StringValues remainingTimeString))
        {
            return TypedResults.BadRequest();
        }

        TimeSpan remainingTime;
        try
        {
            remainingTime = TimeFormatter.ParseDuration(remainingTimeString.ToString());
            if (remainingTime < TimeSpan.Zero)
            {
                return TypedResults.BadRequest();
            }
        }
        catch
        {
            return TypedResults.BadRequest();
        }

        var finishPrice = CraftingCalculator.CalculateFinishPrice(remainingTime);

        return EarthJson(new FinishPrice(finishPrice.Price, 0, TimeFormatter.FormatDuration(finishPrice.ValidFor)));
    }

    [HttpGet("smelting/finish/price")]
    public Results<ContentHttpResult, BadRequest> GetSmeltingPrice()
    {
        if (!Request.Query.TryGetValue("remainingTime", out StringValues remainingTimeString))
        {
            return TypedResults.BadRequest();
        }

        TimeSpan remainingTime;
        try
        {
            remainingTime = TimeFormatter.ParseDuration(remainingTimeString.ToString());
            if (remainingTime < TimeSpan.Zero)
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
        if (!TryGetAccountId(out var accountId) || slotIndex is < 1 or > 3)
        {
            return TypedResults.BadRequest();
        }

        ExpectedPurchasePriceR? expectedPurchasePrice = await Request.Body.AsJsonAsync(AppJsonContext.Default.ExpectedPurchasePriceR, cancellationToken);
        if (expectedPurchasePrice is null || expectedPurchasePrice.ExpectedPurchasePrice < 0)
        {
            return TypedResults.BadRequest();
        }

        var craftingSlots = await _earthDb.CraftingSlots
                  .AsTracking()
                  .FirstAsync(craftingSlots => craftingSlots.Id == accountId, cancellationToken: cancellationToken);

        var profile = await _earthDb.Profiles
            .AsTracking()
            .FirstAsync(profile => profile.Id == accountId, cancellationToken: cancellationToken);

        var craftingSlot = craftingSlots.Slots[slotIndex - 1];

        if (!craftingSlot.Locked)
        {
            return EarthJson(new Dictionary<string, object>(StringComparer.Ordinal), new EarthApiResponse.UpdatesResponse());
        }

        var unlockPrice = CraftingCalculator.CalculateUnlockPrice(slotIndex);

        if (expectedPurchasePrice.ExpectedPurchasePrice != unlockPrice)
        {
            return EarthJson(new Dictionary<string, object>(StringComparer.Ordinal), new EarthApiResponse.UpdatesResponse());
        }

        if (!profile.Rubies.Spend(unlockPrice))
        {
            return EarthJson(new Dictionary<string, object>(StringComparer.Ordinal), new EarthApiResponse.UpdatesResponse());
        }

        craftingSlot.Locked = false;

        await _earthDb.SaveChangesAsync(cancellationToken);

        var results = new ResultsEF.Builder()
            .Crafting()
            .Profile();

        return EarthJson(new Dictionary<string, object>(StringComparer.Ordinal), new EarthApiResponse.UpdatesResponse(await results.BuildAsync(_earthDb, accountId, cancellationToken)));
    }

    [HttpPost("smelting/{slotIndex}/unlock")]
    public async Task<Results<ContentHttpResult, BadRequest>> UnlockSmeltingSlot(int slotIndex, CancellationToken cancellationToken)
    {
        if (!TryGetAccountId(out var accountId) || slotIndex is < 1 or > 3)
        {
            return TypedResults.BadRequest();
        }

        ExpectedPurchasePriceR? expectedPurchasePrice = await Request.Body.AsJsonAsync(AppJsonContext.Default.ExpectedPurchasePriceR, cancellationToken);
        if (expectedPurchasePrice is null || expectedPurchasePrice.ExpectedPurchasePrice < 0)
        {
            return TypedResults.BadRequest();
        }

        var smeltingSlots = await _earthDb.SmeltingSlots
            .AsTracking()
            .FirstAsync(smeltingSlots => smeltingSlots.Id == accountId, cancellationToken: cancellationToken);

        var profile = await _earthDb.Profiles
            .AsTracking()
            .FirstAsync(profile => profile.Id == accountId, cancellationToken: cancellationToken);

        var smeltingSlot = smeltingSlots.Slots[slotIndex - 1];

        if (!smeltingSlot.Locked)
        {
            return EarthJson(new Dictionary<string, object>(StringComparer.Ordinal), new EarthApiResponse.UpdatesResponse());
        }

        var unlockPrice = SmeltingCalculator.CalculateUnlockPrice(slotIndex);

        if (expectedPurchasePrice.ExpectedPurchasePrice != unlockPrice)
        {
            return EarthJson(new Dictionary<string, object>(StringComparer.Ordinal), new EarthApiResponse.UpdatesResponse());
        }

        if (!profile.Rubies.Spend(unlockPrice))
        {
            return EarthJson(new Dictionary<string, object>(StringComparer.Ordinal), new EarthApiResponse.UpdatesResponse());
        }

        smeltingSlot.Locked = false;

        await _earthDb.SaveChangesAsync(cancellationToken);

        var results = new ResultsEF.Builder()
            .Smelting()
            .Profile();

        return EarthJson(new Dictionary<string, object>(StringComparer.Ordinal), new EarthApiResponse.UpdatesResponse(await results.BuildAsync(_earthDb, accountId, cancellationToken)));
    }

    private Types.Workshop.CraftingSlot CraftingSlotModelToResponseIncludingLocked(CraftingSlot craftingSlotModel, DateTimeOffset currentTime, int streamVersion, int slotIndex)
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

    private Types.Workshop.CraftingSlot CraftingSlotModelToResponse(CraftingSlot craftingSlotModel, DateTimeOffset currentTime, int streamVersion)
    {
        if (craftingSlotModel.Locked)
        {
            throw new ArgumentException($"{nameof(craftingSlotModel)} is locked.", nameof(craftingSlotModel));
        }

        CraftingSlot.ActiveCraftingJob? activeJob = craftingSlotModel.ActiveJob;
        if (activeJob is not null)
        {
            CraftingCalculator.State state = CraftingCalculator.CalculateState(currentTime, activeJob, _staticData.Catalog);
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

    private Types.Workshop.SmeltingSlot SmeltingSlotModelToResponseIncludingLocked(SmeltingSlotEF smeltingSlotModel, DateTimeOffset currentTime, int streamVersion, int slotIndex)
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

    private Types.Workshop.SmeltingSlot SmeltingSlotModelToResponse(SmeltingSlotEF smeltingSlotModel, DateTimeOffset currentTime, int streamVersion)
    {
        if (smeltingSlotModel.Locked)
        {
            throw new ArgumentException($"{nameof(smeltingSlotModel)} is locked.", nameof(smeltingSlotModel));
        }

        SmeltingSlotEF.ActiveSmeltingJob? activeJob = smeltingSlotModel.ActiveJob;
        if (activeJob is not null)
        {
            SmeltingCalculator.State state = SmeltingCalculator.CalculateState(currentTime, activeJob, smeltingSlotModel.Burning, _staticData.Catalog);

            Types.Workshop.SmeltingSlot.FuelR? fuel;
            if (state.RemainingAddedFuel is not null && state.RemainingAddedFuel.Item.Count > 0)
            {
                fuel = new Types.Workshop.SmeltingSlot.FuelR(
                    new BurnRate((int)state.RemainingAddedFuel.BurnDuration.TotalMilliseconds, state.RemainingAddedFuel.HeatPerSecond),
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
                (float)state.CurrentBurningFuel.BurnDuration.TotalMilliseconds * state.CurrentBurningFuel.HeatPerSecond - state.RemainingHeat,
                new Types.Workshop.SmeltingSlot.FuelR(
                    new BurnRate((int)state.CurrentBurningFuel.BurnDuration.TotalMilliseconds, state.CurrentBurningFuel.HeatPerSecond),
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
            SmeltingSlotEF.BurningR? burningModel = smeltingSlotModel.Burning;
            Types.Workshop.SmeltingSlot.BurningR? burning = burningModel is not null ? new Types.Workshop.SmeltingSlot.BurningR(
                null,
                null,
                TimeFormatter.FormatDuration(burningModel.RemainingHeat * 1000 / burningModel.Fuel.HeatPerSecond),
                (float)burningModel.Fuel.BurnDuration.TotalMilliseconds * burningModel.Fuel.HeatPerSecond * burningModel.Fuel.Item.Count - burningModel.RemainingHeat,
                new Types.Workshop.SmeltingSlot.FuelR(
                    new BurnRate((int)burningModel.Fuel.BurnDuration.TotalMilliseconds, burningModel.Fuel.HeatPerSecond),
                    burningModel.Fuel.Item.Id,
                    burningModel.Fuel.Item.Count,
                    [.. burningModel.Fuel.Item.Instances.Select(item => item.InstanceId)]
                )
            ) : null;
            return new Types.Workshop.SmeltingSlot(null, burning, null, null, null, null, 0, 0, 0, null, null, State.EMPTY, null, null, streamVersion);
        }
    }

    internal sealed record StartRequestCrafting(
        string SessionId,
        Guid RecipeId,
        int Multiplier,
        StartRequestCraftingItem[] Ingredients
    );

    internal sealed record StartRequestCraftingItem(
        Guid ItemId,
        int Quantity,
        Guid[] ItemInstanceIds
    );

    internal sealed record StartRequestSmelting(
        string SessionId,
        Guid RecipeId,
        int Multiplier,
        StartRequestSmeltingItem Input,
        StartRequestSmeltingItem Fuel
    );

    internal sealed record StartRequestSmeltingItem(
        Guid ItemId,
        int Quantity,
        Guid[] ItemInstanceIds
    );
}
