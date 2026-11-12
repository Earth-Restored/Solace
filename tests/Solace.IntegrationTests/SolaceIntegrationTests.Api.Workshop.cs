using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Solace.Db.Earth;
using Solace.Db.Earth.Models.Player;

namespace Solace.IntegrationTests;

public sealed partial class SolaceIntegrationTests
{
    private static readonly Guid RedstoneBlockItemId = Guid.Parse("42431c81-8c9e-87c5-6a3d-47344931df53");
    private static readonly Guid RedstoneCraftingRecipeId = Guid.Parse("15a3a26d-295b-452b-bdf0-6fe1c53bcbf2");

    private static readonly Guid IronOreItemId = Guid.Parse("efea9c90-e554-3f2a-4d0e-bfd754d899f9");
    private static readonly Guid CoalItemId = Guid.Parse("d7ba8872-ed17-2591-a353-4705c77dfd4d");
    private static readonly Guid IronSmeltingRecipeId = Guid.Parse("4cf90212-9c97-42bf-8c21-8ca34a4eec79");

    [Test]
    public async Task Workshop_GetUtilityBlocks_ReturnsCraftingAndSmeltingSlots(CancellationToken cancellationToken)
    {
        using var client = await CreateAuthenticatedApiClientAsync(cancellationToken);
        using var response = await client.GetAsync("/1/api/v1.1/player/utilityBlocks", cancellationToken);

        await Assert.That(response).IsOk();

        var json = await response.Content.ReadFromJsonAsync<JsonObject>(cancellationToken);
        await Assert.That(json).IsNotNull();

        var result = (json!["result"] ?? json["Result"]) as JsonObject;
        await Assert.That(result).IsNotNull();

        var crafting = (result!["crafting"] ?? result["Crafting"]) as JsonObject;
        await Assert.That(crafting).IsNotNull();

        var slot1Crafting = (crafting!["1"] ?? crafting["Slot1"]) as JsonObject;
        await Assert.That(slot1Crafting).IsNotNull();

        var slot1CraftingState = (slot1Crafting!["state"] ?? slot1Crafting["State"])?.GetValue<string>();
        await Assert.That(slot1CraftingState).IsNotEqualTo("Locked");

        var smelting = (result["smelting"] ?? result["Smelting"]) as JsonObject;
        await Assert.That(smelting).IsNotNull();

        var slot1Smelting = (smelting!["1"] ?? smelting["Slot1"]) as JsonObject;
        await Assert.That(slot1Smelting).IsNotNull();

        var slot1SmeltingState = (slot1Smelting!["state"] ?? slot1Smelting["State"])?.GetValue<string>();
        await Assert.That(slot1SmeltingState).IsNotEqualTo("Locked");
    }

    [Test]
    public async Task Workshop_GetSlotStatus_InvalidSlot_ReturnsBadRequest(CancellationToken cancellationToken)
    {
        using var client = await CreateAuthenticatedApiClientAsync(cancellationToken);

        using var craft0 = await client.GetAsync("/1/api/v1.1/crafting/0", cancellationToken);
        await Assert.That(craft0.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);

        using var craft4 = await client.GetAsync("/1/api/v1.1/crafting/4", cancellationToken);
        await Assert.That(craft4.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);

        using var smelt0 = await client.GetAsync("/1/api/v1.1/smelting/0", cancellationToken);
        await Assert.That(smelt0.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);

        using var smelt4 = await client.GetAsync("/1/api/v1.1/smelting/4", cancellationToken);
        await Assert.That(smelt4.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task Workshop_GetFinishPrice_ReturnsCalculatedPrice(CancellationToken cancellationToken)
    {
        using var client = await CreateAuthenticatedApiClientAsync(cancellationToken);

        using var craftPriceResponse = await client.GetAsync("/1/api/v1.1/crafting/finish/price?remainingTime=0:05:00", cancellationToken);
        await Assert.That(craftPriceResponse).IsOk();

        var craftJson = await craftPriceResponse.Content.ReadFromJsonAsync<JsonObject>(cancellationToken);
        var craftResult = (craftJson!["result"] ?? craftJson["Result"]) as JsonObject;
        await Assert.That(craftResult).IsNotNull();

        var craftCost = (craftResult!["cost"] ?? craftResult["Cost"])?.GetValue<int>();
        await Assert.That(craftCost).IsNotNull();
        await Assert.That(craftCost!.Value).IsGreaterThan(0);

        using var smeltPriceResponse = await client.GetAsync("/1/api/v1.1/smelting/finish/price?remainingTime=0:05:00", cancellationToken);
        await Assert.That(smeltPriceResponse).IsOk();

        var smeltJson = await smeltPriceResponse.Content.ReadFromJsonAsync<JsonObject>(cancellationToken);
        var smeltResult = (smeltJson!["result"] ?? smeltJson["Result"]) as JsonObject;
        await Assert.That(smeltResult).IsNotNull();

        var smeltCost = (smeltResult!["cost"] ?? smeltResult["Cost"])?.GetValue<int>();
        await Assert.That(smeltCost).IsNotNull();
        await Assert.That(smeltCost!.Value).IsGreaterThan(0);

        // missing query param returns 400
        using var badPriceResponse = await client.GetAsync("/1/api/v1.1/crafting/finish/price", cancellationToken);
        await Assert.That(badPriceResponse.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task Workshop_Crafting_Lifecycle_StartCheckAndStop(CancellationToken cancellationToken)
    {
        await using (var earthDb = EarthDbContext.CreateFromConnection(_fixture.EarthConnectionString))
        {
            var item = await earthDb.StackableItems
                .FirstOrDefaultAsync(i => i.ProfileId == _fixture.ProfileId && i.ItemId == RedstoneBlockItemId, cancellationToken);

            if (item is not null)
            {
                item.Count += 5;
            }
            else
            {
                earthDb.StackableItems.Add(new StackableItemEF(_fixture.ProfileId, RedstoneBlockItemId, 5));
            }

            var craftingSlots = await earthDb.CraftingSlots
                .FirstAsync(cs => cs.Id == _fixture.ProfileId, cancellationToken);
            craftingSlots.Slots[0].ActiveJob = null;

            await earthDb.SaveChangesAsync(cancellationToken);
        }

        using var client = await CreateAuthenticatedApiClientAsync(cancellationToken);

        var startPayload = $$"""
            {
                "SessionId": "test-crafting-session",
                "RecipeId": "{{RedstoneCraftingRecipeId}}",
                "Multiplier": 1,
                "Ingredients": [
                    {
                        "ItemId": "{{RedstoneBlockItemId}}",
                        "Quantity": 1,
                        "ItemInstanceIds": []
                    }
                ]
            }
            """;

        using var startResponse = await client.PostAsync(
            "/1/api/v1.1/crafting/1/start",
            new StringContent(startPayload, Encoding.UTF8, "application/json"),
            cancellationToken);

        await Assert.That(startResponse).IsOk();

        using var statusResponse = await client.GetAsync("/1/api/v1.1/crafting/1", cancellationToken);
        await Assert.That(statusResponse).IsOk();

        var statusJson = await statusResponse.Content.ReadFromJsonAsync<JsonObject>(cancellationToken);
        var statusResult = (statusJson!["result"] ?? statusJson["Result"]) as JsonObject;
        await Assert.That(statusResult).IsNotNull();

        var recipeId = (statusResult!["recipeId"] ?? statusResult["RecipeId"])?.GetValue<string>();
        await Assert.That(recipeId).IsEqualTo(RedstoneCraftingRecipeId.ToString());

        var state = (statusResult["state"] ?? statusResult["State"])?.GetValue<string>();
        await Assert.That(state).IsEqualTo("Active");

        using var stopResponse = await client.PostAsync("/1/api/v1.1/crafting/1/stop", null, cancellationToken);
        await Assert.That(stopResponse).IsOk();

        var stopJson = await stopResponse.Content.ReadFromJsonAsync<JsonObject>(cancellationToken);
        var stopResult = (stopJson!["result"] ?? stopJson["Result"]) as JsonObject;
        await Assert.That(stopResult).IsNotNull();

        var stoppedState = (stopResult!["state"] ?? stopResult["State"])?.GetValue<string>();
        await Assert.That(stoppedState).IsEqualTo("Empty");

        using var collectResponse = await client.PostAsync("/1/api/v1.1/crafting/1/collectItems", null, cancellationToken);
        await Assert.That(collectResponse).IsOk();
    }

    [Test]
    public async Task Workshop_Smelting_Lifecycle_StartCheckAndStop(CancellationToken cancellationToken)
    {
        await using (var earthDb = EarthDbContext.CreateFromConnection(_fixture.EarthConnectionString))
        {
            var ironOre = await earthDb.StackableItems
                .FirstOrDefaultAsync(i => i.ProfileId == _fixture.ProfileId && i.ItemId == IronOreItemId, cancellationToken);
            if (ironOre is not null)
            {
                ironOre.Count += 5;
            }
            else
            {
                earthDb.StackableItems.Add(new StackableItemEF(_fixture.ProfileId, IronOreItemId, 5));
            }

            var coal = await earthDb.StackableItems
                .FirstOrDefaultAsync(i => i.ProfileId == _fixture.ProfileId && i.ItemId == CoalItemId, cancellationToken);
            if (coal is not null)
            {
                coal.Count += 5;
            }
            else
            {
                earthDb.StackableItems.Add(new StackableItemEF(_fixture.ProfileId, CoalItemId, 5));
            }

            var smeltingSlots = await earthDb.SmeltingSlots
                .FirstAsync(ss => ss.Id == _fixture.ProfileId, cancellationToken);
            smeltingSlots.Slots[0].ActiveJob = null;

            await earthDb.SaveChangesAsync(cancellationToken);
        }

        using var client = await CreateAuthenticatedApiClientAsync(cancellationToken);

        var startPayload = $$"""
            {
                "SessionId": "test-smelting-session",
                "RecipeId": "{{IronSmeltingRecipeId}}",
                "Multiplier": 1,
                "Input": {
                    "ItemId": "{{IronOreItemId}}",
                    "Quantity": 1,
                    "ItemInstanceIds": []
                },
                "Fuel": {
                    "ItemId": "{{CoalItemId}}",
                    "Quantity": 1,
                    "ItemInstanceIds": []
                }
            }
            """;

        using var startResponse = await client.PostAsync(
            "/1/api/v1.1/smelting/1/start",
            new StringContent(startPayload, Encoding.UTF8, "application/json"),
            cancellationToken);

        await Assert.That(startResponse).IsOk();

        using var statusResponse = await client.GetAsync("/1/api/v1.1/smelting/1", cancellationToken);
        await Assert.That(statusResponse).IsOk();

        var statusJson = await statusResponse.Content.ReadFromJsonAsync<JsonObject>(cancellationToken);
        var statusResult = (statusJson!["result"] ?? statusJson["Result"]) as JsonObject;
        await Assert.That(statusResult).IsNotNull();

        var recipeId = (statusResult!["recipeId"] ?? statusResult["RecipeId"])?.GetValue<string>();
        await Assert.That(recipeId).IsEqualTo(IronSmeltingRecipeId.ToString());

        var state = (statusResult["state"] ?? statusResult["State"])?.GetValue<string>();
        await Assert.That(state).IsEqualTo("Active");

        using var stopResponse = await client.PostAsync("/1/api/v1.1/smelting/1/stop", null, cancellationToken);
        await Assert.That(stopResponse).IsOk();

        var stopJson = await stopResponse.Content.ReadFromJsonAsync<JsonObject>(cancellationToken);
        var stopResult = (stopJson!["result"] ?? stopJson["Result"]) as JsonObject;
        await Assert.That(stopResult).IsNotNull();

        var stoppedState = (stopResult!["state"] ?? stopResult["State"])?.GetValue<string>();
        await Assert.That(stoppedState).IsEqualTo("Empty");

        using var collectResponse = await client.PostAsync("/1/api/v1.1/smelting/1/collectItems", null, cancellationToken);
        await Assert.That(collectResponse).IsOk();
    }

    [Test]
    public async Task Workshop_UnlockSlot_InvalidExpectedPrice_NoOp(CancellationToken cancellationToken)
    {
        using var client = await CreateAuthenticatedApiClientAsync(cancellationToken);

        var payload = """
            {
                "ExpectedPurchasePrice": 0
            }
            """;

        using var response = await client.PostAsync(
            "/1/api/v1.1/crafting/2/unlock",
            new StringContent(payload, Encoding.UTF8, "application/json"),
            cancellationToken);

        await Assert.That(response).IsOk();

        using var slot2Response = await client.GetAsync("/1/api/v1.1/crafting/2", cancellationToken);
        await Assert.That(slot2Response).IsOk();

        var json = await slot2Response.Content.ReadFromJsonAsync<JsonObject>(cancellationToken);
        var result = (json!["result"] ?? json["Result"]) as JsonObject;
        await Assert.That(result).IsNotNull();

        var state = (result!["state"] ?? result["State"])?.GetValue<string>();
        await Assert.That(state).IsEqualTo("Locked");
    }

    [Test]
    public async Task Workshop_UnlockSlot_ValidPrice_UnlocksSlotAndSpendsRubies(CancellationToken cancellationToken)
    {
        await using (var earthDb = EarthDbContext.CreateFromConnection(_fixture.EarthConnectionString))
        {
            var profile = await earthDb.Profiles.FirstAsync(p => p.Id == _fixture.ProfileId, cancellationToken);
            profile.Rubies.Earned = int.Max(profile.Rubies.Earned, 50);

            var craftingSlots = await earthDb.CraftingSlots.FirstAsync(cs => cs.Id == _fixture.ProfileId, cancellationToken);
            craftingSlots.Slots[1].Locked = true;

            var smeltingSlots = await earthDb.SmeltingSlots.FirstAsync(ss => ss.Id == _fixture.ProfileId, cancellationToken);
            smeltingSlots.Slots[1].Locked = true;

            await earthDb.SaveChangesAsync(cancellationToken);
        }

        using var client = await CreateAuthenticatedApiClientAsync(cancellationToken);

        using var rubiesBeforeResponse = await client.GetAsync("/1/api/v1.1/player/rubies", cancellationToken);
        await Assert.That(rubiesBeforeResponse).IsOk();
        var rubiesBeforeJson = await rubiesBeforeResponse.Content.ReadFromJsonAsync<JsonObject>(cancellationToken);
        var initialRubies = (rubiesBeforeJson!["result"] ?? rubiesBeforeJson["Result"])!.GetValue<int>();

        var unlockPayload = """
            {
                "ExpectedPurchasePrice": 10
            }
            """;

        using var unlockCraftResponse = await client.PostAsync(
            "/1/api/v1.1/crafting/2/unlock",
            new StringContent(unlockPayload, Encoding.UTF8, "application/json"),
            cancellationToken);

        await Assert.That(unlockCraftResponse).IsOk();

        using var slot2Response = await client.GetAsync("/1/api/v1.1/crafting/2", cancellationToken);
        await Assert.That(slot2Response).IsOk();
        var slot2Json = await slot2Response.Content.ReadFromJsonAsync<JsonObject>(cancellationToken);
        var slot2Result = (slot2Json!["result"] ?? slot2Json["Result"]) as JsonObject;
        var craftState = (slot2Result!["state"] ?? slot2Result["State"])?.GetValue<string>();
        await Assert.That(craftState).IsEqualTo("Empty");

        using var unlockSmeltResponse = await client.PostAsync(
            "/1/api/v1.1/smelting/2/unlock",
            new StringContent(unlockPayload, Encoding.UTF8, "application/json"),
            cancellationToken);

        await Assert.That(unlockSmeltResponse).IsOk();

        using var smelt2Response = await client.GetAsync("/1/api/v1.1/smelting/2", cancellationToken);
        await Assert.That(smelt2Response).IsOk();
        var smelt2Json = await smelt2Response.Content.ReadFromJsonAsync<JsonObject>(cancellationToken);
        var smelt2Result = (smelt2Json!["result"] ?? smelt2Json["Result"]) as JsonObject;
        var smeltState = (smelt2Result!["state"] ?? smelt2Result["State"])?.GetValue<string>();
        await Assert.That(smeltState).IsEqualTo("Empty");

        using var rubiesAfterResponse = await client.GetAsync("/1/api/v1.1/player/rubies", cancellationToken);
        await Assert.That(rubiesAfterResponse).IsOk();
        var rubiesAfterJson = await rubiesAfterResponse.Content.ReadFromJsonAsync<JsonObject>(cancellationToken);
        var finalRubies = (rubiesAfterJson!["result"] ?? rubiesAfterJson["Result"])!.GetValue<int>();
        await Assert.That(finalRubies).IsEqualTo(initialRubies - 20);
    }
}
