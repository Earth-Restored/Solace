using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Solace.Db.Earth;
using Solace.Db.Playfab;
using Solace.Db.Playfab.Models.Items;

namespace Solace.IntegrationTests;

public sealed partial class SolaceIntegrationTests
{
    private static readonly Guid StoreBreadItemId = Guid.Parse("111637af-d42d-133c-cefe-184442d230ca");

    [Test]
    public async Task Store_GetStoreItemInfo_ReturnsItemStatus(CancellationToken cancellationToken)
    {
        using var client = await CreateAuthenticatedApiClientAsync(cancellationToken);

        var payload = """
            [
                {
                    "Id": "00000000-0000-0000-0000-000000000001",
                    "StoreItemType": "Buildplates",
                    "StreamVersion": 1
                }
            ]
            """;

        using var response = await client.PostAsync(
            "/1/api/v1.1/commerce/storeItemInfo",
            new StringContent(payload, Encoding.UTF8, "application/json"),
            cancellationToken);

        await Assert.That(response).IsOk();

        var json = await response.Content.ReadFromJsonAsync<JsonObject>(cancellationToken);
        await Assert.That(json).IsNotNull();

        var result = (json!["result"] ?? json["Result"]) as JsonArray;
        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Count).IsEqualTo(1);

        var item = result[0] as JsonObject;
        await Assert.That(item).IsNotNull();

        var status = (item!["status"] ?? item["Status"])?.GetValue<string>();
        await Assert.That(status).IsEqualTo("NotFound");
    }

    [Test]
    public async Task Store_Purchase_InvalidItem_ReturnsBadRequest(CancellationToken cancellationToken)
    {
        using var client = await CreateAuthenticatedApiClientAsync(cancellationToken);
        var randomItemId = Guid.NewGuid();

        var payload = $$"""
            {
                "ExpectedPurchasePrice": 10,
                "ItemId": "{{randomItemId}}"
            }
            """;

        using var purchaseResponse = await client.PostAsync(
            "/1/api/v1.1/commerce/purchase",
            new StringContent(payload, Encoding.UTF8, "application/json"),
            cancellationToken);

        await Assert.That(purchaseResponse.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);

        using var purchaseV2Response = await client.PostAsync(
            "/1/api/v1.1/commerce/purchaseV2",
            new StringContent(payload, Encoding.UTF8, "application/json"),
            cancellationToken);

        await Assert.That(purchaseV2Response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task Store_PurchaseV2_InventoryItem_DeductsRubiesAndAddsItems(CancellationToken cancellationToken)
    {
        var storeItemId = Guid.NewGuid();
        const int itemCost = 10;
        const int itemAmount = 2;

        await using (var playfabDb = PlayfabDbContext.CreateFromConnection(_fixture.PlayfabConnectionString))
        {
            var item = new ItemEF
            {
                Id = storeItemId,
                FriendlyId = Guid.NewGuid(),
                Title = "test_apple_item",
                Purchasable = true,
                StartDate = DateTimeOffset.UtcNow.AddMinutes(-5),
                Data = new InventoryItemDataEF
                {
#pragma warning disable CS0618
                    Id = storeItemId,
#pragma warning restore CS0618
                    ItemId = StoreBreadItemId,
                    Cost = itemCost,
                    Amount = itemAmount,
                    Rarity = RarityEF.Common,
                }
            };
            item.Data.Item = item;

            playfabDb.Items.Add(item);
            await playfabDb.SaveChangesAsync(cancellationToken);
        }

        int initialRubies;
        int initialBreadCount = 0;
        await using (var earthDb = EarthDbContext.CreateFromConnection(_fixture.EarthConnectionString))
        {
            var profile = await earthDb.Profiles.FirstAsync(p => p.Id == _fixture.ProfileId, cancellationToken);
            profile.Rubies.Earned = int.Max(profile.Rubies.Earned, 500);
            initialRubies = profile.Rubies.Total;

            var existingItem = await earthDb.StackableItems
                .FirstOrDefaultAsync(i => i.ProfileId == _fixture.ProfileId && i.ItemId == StoreBreadItemId, cancellationToken);
            if (existingItem is not null)
            {
                initialBreadCount = existingItem.Count;
            }

            await earthDb.SaveChangesAsync(cancellationToken);
        }

        using var client = await CreateAuthenticatedApiClientAsync(cancellationToken);

        var payload = $$"""
            {
                "ExpectedPurchasePrice": {{itemCost}},
                "ItemId": "{{storeItemId}}"
            }
            """;

        using var response = await client.PostAsync(
            "/1/api/v1.1/commerce/purchaseV2",
            new StringContent(payload, Encoding.UTF8, "application/json"),
            cancellationToken);

        await Assert.That(response).IsOk();

        var json = await response.Content.ReadFromJsonAsync<JsonObject>(cancellationToken);
        await Assert.That(json).IsNotNull();

        var result = (json!["result"] ?? json["Result"]) as JsonObject;
        await Assert.That(result).IsNotNull();

        await using (var earthDb = EarthDbContext.CreateFromConnection(_fixture.EarthConnectionString))
        {
            var profile = await earthDb.Profiles.AsNoTracking().FirstAsync(p => p.Id == _fixture.ProfileId, cancellationToken);
            await Assert.That(profile.Rubies.Total).IsEqualTo(initialRubies - itemCost);

            var breadItem = await earthDb.StackableItems
                .AsNoTracking()
                .FirstAsync(i => i.ProfileId == _fixture.ProfileId && i.ItemId == StoreBreadItemId, cancellationToken);
            await Assert.That(breadItem.Count).IsEqualTo(initialBreadCount + itemAmount);
        }
    }

    [Test]
    public async Task Store_Purchase_NotPurchasable_ReturnsBadRequest(CancellationToken cancellationToken)
    {
        var storeItemId = Guid.NewGuid();
        const int itemCost = 15;

        await using (var playfabDb = PlayfabDbContext.CreateFromConnection(_fixture.PlayfabConnectionString))
        {
            var item = new ItemEF
            {
                Id = storeItemId,
                FriendlyId = Guid.NewGuid(),
                Title = "test_not_purchasable_item",
                Purchasable = false,
                StartDate = DateTimeOffset.UtcNow.AddMinutes(-10),
                Data = new InventoryItemDataEF
                {
#pragma warning disable CS0618
                    Id = storeItemId,
#pragma warning restore CS0618
                    ItemId = StoreBreadItemId,
                    Cost = itemCost,
                    Amount = 1,
                    Rarity = RarityEF.Common,
                }
            };
            item.Data.Item = item;

            playfabDb.Items.Add(item);
            await playfabDb.SaveChangesAsync(cancellationToken);
        }

        using var client = await CreateAuthenticatedApiClientAsync(cancellationToken);
        var payload = $$"""
            {
                "ExpectedPurchasePrice": {{itemCost}},
                "ItemId": "{{storeItemId}}"
            }
            """;

        using var purchaseResponse = await client.PostAsync(
            "/1/api/v1.1/commerce/purchase",
            new StringContent(payload, Encoding.UTF8, "application/json"),
            cancellationToken);

        await Assert.That(purchaseResponse.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);

        using var purchaseV2Response = await client.PostAsync(
            "/1/api/v1.1/commerce/purchaseV2",
            new StringContent(payload, Encoding.UTF8, "application/json"),
            cancellationToken);

        await Assert.That(purchaseV2Response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task Store_Purchase_FutureStartDate_ReturnsBadRequest(CancellationToken cancellationToken)
    {
        var storeItemId = Guid.NewGuid();
        const int itemCost = 20;

        await using (var playfabDb = PlayfabDbContext.CreateFromConnection(_fixture.PlayfabConnectionString))
        {
            var item = new ItemEF
            {
                Id = storeItemId,
                FriendlyId = Guid.NewGuid(),
                Title = "test_future_item",
                Purchasable = true,
                StartDate = DateTimeOffset.UtcNow.AddDays(7),
                Data = new InventoryItemDataEF
                {
#pragma warning disable CS0618
                    Id = storeItemId,
#pragma warning restore CS0618
                    ItemId = StoreBreadItemId,
                    Cost = itemCost,
                    Amount = 1,
                    Rarity = RarityEF.Common,
                }
            };
            item.Data.Item = item;

            playfabDb.Items.Add(item);
            await playfabDb.SaveChangesAsync(cancellationToken);
        }

        using var client = await CreateAuthenticatedApiClientAsync(cancellationToken);
        var payload = $$"""
            {
                "ExpectedPurchasePrice": {{itemCost}},
                "ItemId": "{{storeItemId}}"
            }
            """;

        using var purchaseResponse = await client.PostAsync(
            "/1/api/v1.1/commerce/purchase",
            new StringContent(payload, Encoding.UTF8, "application/json"),
            cancellationToken);

        await Assert.That(purchaseResponse.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);

        using var purchaseV2Response = await client.PostAsync(
            "/1/api/v1.1/commerce/purchaseV2",
            new StringContent(payload, Encoding.UTF8, "application/json"),
            cancellationToken);

        await Assert.That(purchaseV2Response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task Store_Purchase_PriceMismatch_ReturnsBadRequest(CancellationToken cancellationToken)
    {
        var storeItemId = Guid.NewGuid();
        const int itemCost = 50;
        const int wrongPrice = 10;

        await using (var playfabDb = PlayfabDbContext.CreateFromConnection(_fixture.PlayfabConnectionString))
        {
            var item = new ItemEF
            {
                Id = storeItemId,
                FriendlyId = Guid.NewGuid(),
                Title = "test_price_mismatch_item",
                Purchasable = true,
                StartDate = DateTimeOffset.UtcNow.AddMinutes(-5),
                Data = new InventoryItemDataEF
                {
#pragma warning disable CS0618
                    Id = storeItemId,
#pragma warning restore CS0618
                    ItemId = StoreBreadItemId,
                    Cost = itemCost,
                    Amount = 1,
                    Rarity = RarityEF.Common,
                }
            };
            item.Data.Item = item;

            playfabDb.Items.Add(item);
            await playfabDb.SaveChangesAsync(cancellationToken);
        }

        using var client = await CreateAuthenticatedApiClientAsync(cancellationToken);
        var payload = $$"""
            {
                "ExpectedPurchasePrice": {{wrongPrice}},
                "ItemId": "{{storeItemId}}"
            }
            """;

        using var purchaseResponse = await client.PostAsync(
            "/1/api/v1.1/commerce/purchase",
            new StringContent(payload, Encoding.UTF8, "application/json"),
            cancellationToken);

        await Assert.That(purchaseResponse.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);

        using var purchaseV2Response = await client.PostAsync(
            "/1/api/v1.1/commerce/purchaseV2",
            new StringContent(payload, Encoding.UTF8, "application/json"),
            cancellationToken);

        await Assert.That(purchaseV2Response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }
}
