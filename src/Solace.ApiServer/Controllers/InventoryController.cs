using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using Solace.ApiServer.Types.Inventory;
using Solace.ApiServer.Utils;
using Solace.Common;
using Solace.Common.Utils;
using Solace.Db.Earth;
using Solace.Db.Earth.Models.Player;
using Solace.StaticData;
using Microsoft.EntityFrameworkCore;

namespace Solace.ApiServer.Controllers;

[Authorize]
[ApiVersion("1.1")]
[Route("1/api/v{version:apiVersion}/inventory/survival")]
internal sealed class InventoryController : SolaceControllerBase
{
    private readonly EarthDbContext _earthDb;
    private readonly Catalog _catalog;
    private readonly ILogger<InventoryController> _logger;

    public InventoryController(EarthDbContext earthDB, StaticData.StaticDataProvider staticData, ILogger<InventoryController> logger)
    {
        _earthDb = earthDB;
        _catalog = staticData.Catalog;
        _logger = logger;
    }

    [HttpGet]
    public async Task<Results<ContentHttpResult, BadRequest>> GetInventory(CancellationToken cancellationToken)
    {
        if (!TryGetAccountId(out var accountId))
        {
            return TypedResults.BadRequest();
        }

        var stackableItems = await _earthDb.StackableItems
            .AsNoTracking()
            .Where(item => item.AccountId == accountId)
            .ToListAsync(cancellationToken);

        var nonStackableItems = await _earthDb.NonStackableItems
            .AsNoTracking()
            .Where(item => item.AccountId == accountId)
            .GroupBy(instance => instance.ItemId)
            .ToListAsync(cancellationToken);

        var hotbar = await _earthDb.Hotbars
            .AsNoTracking()
            .FirstAsync(hotbar => hotbar.Id == accountId, cancellationToken: cancellationToken);

        var journalEntries = await _earthDb.JournalEntries
            .AsNoTracking()
            .Where(entry => entry.AccountId == accountId)
            .ToDictionaryAsync(entry => entry.ItemId, cancellationToken);

        Dictionary<Guid, int> hotbarItemCounts = [];
        foreach (var item in hotbar.Items)
        {
            if (item is not null)
            {
                hotbarItemCounts[item.Uuid] = hotbarItemCounts.GetValueOrDefault(item.Uuid, 0) + item.Count;
            }
        }

        HashSet<Guid> hotbarItemInstances = [];
        foreach (var item in hotbar.Items)
        {
            if (item is not null && item.InstanceId is not null)
            {
                hotbarItemInstances.Add(item.InstanceId.Value);
            }
        }

        var inventoryResponse = new Types.Inventory.InventoryResponse(
            [.. hotbar.Items.Select(item => item is not null ? new HotbarItem(
                item.Uuid,
                item.Count,
                item.InstanceId,
                item.InstanceId is not null
                    ? ItemWear.WearToHealth(item.Uuid, nonStackableItems.FirstOrDefault(nsi => nsi.Key == item.Uuid)?.FirstOrDefault(nsi => nsi.InstanceId == item.InstanceId.Value)?.Wear ?? 0, _catalog.ItemsCatalog)
                    : 0.0f
                ) : null)],
            [.. stackableItems.Select(item =>
            {
                var uuid = item.ItemId;
                var count = item.Count - hotbarItemCounts.GetValueOrDefault(uuid);
                var itemJournalEntry = journalEntries[uuid];
                var firstSeen = TimeFormatter.FormatTime(itemJournalEntry.FirstSeen);
                var lastSeen = TimeFormatter.FormatTime(itemJournalEntry.LastSeen);

                return new StackableInventoryItem(
                    uuid,
                    count,
                    1,
                    new StackableInventoryItem.OnR(firstSeen),
                    new StackableInventoryItem.OnR(lastSeen)
                );
            })],
            [.. nonStackableItems.Select(group =>
            {
                var uuid = group.Key;
                var itemJournalEntry = journalEntries[uuid];
                var firstSeen = TimeFormatter.FormatTime(itemJournalEntry.FirstSeen);
                var lastSeen = TimeFormatter.FormatTime(itemJournalEntry.LastSeen);
                return new NonStackableInventoryItem(
                    uuid,
                    [.. group.Where(instance => !hotbarItemInstances.Contains(instance.InstanceId)).Select(instance => new NonStackableInventoryItem.Instance(instance.InstanceId, ItemWear.WearToHealth(uuid, instance.Wear, _catalog.ItemsCatalog)))],
                    1,
                    new NonStackableInventoryItem.OnR(firstSeen),
                    new NonStackableInventoryItem.OnR(lastSeen)
                );
            })]
        );

        var resp = Json.Serialize(new EarthApiResponse(inventoryResponse));
        return TypedResults.Content(resp, "application/json");
    }

    [HttpPut("hotbar")]
    public async Task<Results<BadRequest, ContentHttpResult>> SetHotbar(CancellationToken cancellationToken)
    {
        if (!TryGetAccountId(out var accountId))
        {
            return TypedResults.BadRequest();
        }

        SetHotbarRequestItem[]? setHotbarRequestItems = await Request.Body.AsJsonAsync(AppJsonContext.Default.SetHotbarRequestItemArray, cancellationToken);
        if (setHotbarRequestItems is null or { Length: not 7, })
        {
            return TypedResults.BadRequest();
        }

        var hotbar = await _earthDb.Hotbars
            .AsTracking()
            .FirstAsync(hotbar => hotbar.Id == accountId, cancellationToken: cancellationToken);

        for (var index = 0; index < hotbar.Items.Length; index++)
        {
            var item = setHotbarRequestItems[index];
            hotbar.Items[index] = item is not null ? new HotbarEF.Item(item.Id, item.Count, item.InstanceId) : null;
        }

        await HotbarUtils.LimitToInventoryAsync(_earthDb, accountId, hotbar, cancellationToken);

        await _earthDb.SaveChangesAsync(cancellationToken);

        HotbarItem?[] hotbarItems = [.. hotbar.Items.Select(item => item is not null ? new HotbarItem(
            item.Uuid,
            item.Count,
            item.InstanceId,
            item.InstanceId is not null ? ItemWear.WearToHealth(item.Uuid, _earthDb.NonStackableItems.AsNoTracking().First(nsi => nsi.AccountId == accountId && nsi.ItemId == item.Uuid && nsi.InstanceId == item.InstanceId.Value).Wear, _catalog.ItemsCatalog) : 0.0f
        ) : null)];

        var resp = Json.Serialize(hotbarItems);
        return TypedResults.Content(resp, "application/json");
    }

    [HttpPost("{itemId}/consume")]
    public async Task<Results<ContentHttpResult, BadRequest>> ConsumeItem(Guid itemId, CancellationToken cancellationToken)
    {
        if (!TryGetAccountId(out var accountId))
        {
            return TypedResults.BadRequest();
        }

        // request.timestamp
        var requestStartedOn = HttpContext.GetTimestamp();

        Catalog.ItemsCatalogR.Item? item = _catalog.ItemsCatalog.GetItem(itemId);

        if (item is null || item.ConsumeInfo is null)
        {
            return TypedResults.BadRequest();
        }

        var profile = await _earthDb.Profiles
            .AsTracking()
            .FirstAsync(profile => profile.Id == accountId, cancellationToken: cancellationToken);

        var boosts = await _earthDb.Boosts
            .AsNoTracking()
            .FirstAsync(boosts => boosts.Id == accountId, cancellationToken: cancellationToken);

        var results = new ResultsEF.Builder();

        if (!await InventoryUtils.TakeStackableItemsAsync(_earthDb, results, accountId, itemId, 1, cancellationToken))
        {
            return TypedResults.Content(Json.Serialize(new EarthApiResponse(null, null)), "application/json");
        }

        var returnItemIdNullable = item.ConsumeInfo.ReturnItemId;
        if (returnItemIdNullable is { } returnItemId)
        {
            var returnItem = _catalog.ItemsCatalog.GetItem(returnItemId);
            Debug.Assert(returnItem is not null);

            if (returnItem.Stackable)
            {
                await InventoryUtils.AddStackableItemsAsync(_earthDb, results, accountId, returnItemId, 1, cancellationToken);
            }
            else
            {
                await InventoryUtils.AddInstanceItemsAsync(_earthDb, results, accountId, returnItemId, 1, cancellationToken);
            }

            if (await JournalUtils.AddCollectedItemAsync(_earthDb, results, accountId, returnItemId, requestStartedOn, 1, cancellationToken) == 0)
            {
                if (returnItem.JournalEntry is not null)
                {
                    await TokenUtils.AddTokenAsync(_earthDb, results, new JournalItemUnlockedTokenEF(accountId, returnItemId), cancellationToken);
                }
            }
        }

        var healing = item.ConsumeInfo.Heal;

        var healingMultiplier = Common.Utils.BoostUtils.GetActiveStatModifiers(boosts, requestStartedOn, _catalog.ItemsCatalog).FoodMultiplier;
        if (healingMultiplier > 0)
        {
            healing = healing * (healingMultiplier + 100) / 100;
        }

        var maxPlayerHealth = Common.Utils.BoostUtils.GetMaxPlayerHealth(boosts, requestStartedOn, _catalog.ItemsCatalog);
        profile.Health += healing;
        if (profile.Health > maxPlayerHealth)
        {
            profile.Health = maxPlayerHealth;
        }

        await _earthDb.SaveChangesAsync(cancellationToken);

        results
            .Inventory()
            .Journal()
            .Profile();

        var resp = Json.Serialize(new EarthApiResponse(null, new EarthApiResponse.UpdatesResponse(await results.BuildAsync(_earthDb, accountId, cancellationToken))));
        return TypedResults.Content(resp, "application/json");
    }

    internal sealed record SetHotbarRequestItem(
        Guid Id,
        int Count,
        Guid? InstanceId
    );
}
