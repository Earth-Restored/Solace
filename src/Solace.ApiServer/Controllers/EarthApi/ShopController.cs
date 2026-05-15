using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Serilog;
using System.Diagnostics;
using System.Security.Claims;
using System.Text;
using Solace.ApiServer.Exceptions;
using Solace.ApiServer.Types.Buildplates;
using Solace.ApiServer.Types.Shop;
using Solace.BuildplateImporter;
using Solace.Common.Utils;
using Solace.DB;
using Solace.DB.Models.Player;
using Solace.ObjectStore.Client;
using Solace.StaticData;
using Solace.EventBus.Client;
using Microsoft.EntityFrameworkCore;
using Solace.DB.Utils;

namespace Solace.ApiServer.Controllers.EarthApi;

[Authorize]
[ApiVersion("1.1")]
[Route("1/api/v{version:apiVersion}/commerce")]
internal sealed class ShopController : SolaceControllerBase
{
    private readonly StaticData.StaticData _staticData;
    private readonly EarthDbContext _earthDB;
    private readonly EventBusClient _eventBus;
    private readonly ObjectStoreClient _objectStore;

    public ShopController(StaticData.StaticData staticData, EarthDbContext earthDB, EventBusClient eventBus, ObjectStoreClient objectStore)
    {
        _staticData = staticData;
        _earthDB = earthDB;
        _eventBus = eventBus;
        _objectStore = objectStore;
    }

    private sealed record StoreItemInfoRequest(string Id, string StoreItemType, uint StreamVersion);

    [HttpPost("storeItemInfo")]
    public async Task<ContentHttpResult> GetStoreItemInfo(CancellationToken cancellationToken)
    {
        var request = await Request.Body.AsJsonAsync<StoreItemInfoRequest[]>(cancellationToken);

        if (request is null or { Length: 0 })
        {
            return EarthJson(Array.Empty<StoreItemInfo>());
        }

        List<StoreItemInfo> result = new(request.Length);

        foreach (var item in request)
        {
            switch (item.StoreItemType)
            {
                case "Buildplates":
                    {
                        var itemId = Guid.Parse(item.Id);

                        var buildplate = await _earthDB.TemplateBuildplates
                            .AsNoTracking()
                            .FirstOrDefaultAsync(template => template.Id == itemId, cancellationToken);

                        StoreItemInfo.StoreItemTypeE storeItemType = Enum.Parse<StoreItemInfo.StoreItemTypeE>(item.StoreItemType);

                        if (buildplate is null)
                        {
                            Log.Warning($"Buildplate with id {item.Id} not found");
                            result.Add(new StoreItemInfo(itemId, storeItemType, StoreItemInfo.StoreItemStatus.NotFound, item.StreamVersion, null, null, null, null, null));
                            break;
                        }

                        byte[]? previewData = await _objectStore.GetAsync(buildplate.PreviewObjectId);

                        if (previewData is null)
                        {
                            Log.Warning($"Failed to get preview for buildplate {item.Id}");
                            result.Add(new StoreItemInfo(itemId, storeItemType, StoreItemInfo.StoreItemStatus.NotFound, item.StreamVersion, null, null, null, null, null));
                            break;
                        }

                        string model = Encoding.ASCII.GetString(previewData);

                        //var itemFromMap = staticData.Catalog.ShopCatalog.Items.GetValueOrDefault(itemId);

                        result.Add(new StoreItemInfo(
                            itemId,
                            storeItemType,
                            StoreItemInfo.StoreItemStatus.Found,
                            item.StreamVersion,
                            model,
                            new Offset(0, buildplate.Offset, 0),
                            new Dimension(buildplate.Size, buildplate.Size),
                            null,
                            null));
                    }

                    break;
            }
        }

        return EarthJson(result);
    }

    private sealed record PurchaseItemRequest(
        int ExpectedPurchasePrice,
        Guid ItemId
    );

    [HttpPost("purchase")]
    public async Task<Results<ContentHttpResult, BadRequest>> Purchase(CancellationToken cancellationToken)
    {
        if (!TryGetAccountId(out var accountId))
        {
            return TypedResults.BadRequest();
        }

        var request = await Request.Body.AsJsonAsync<PurchaseItemRequest>(cancellationToken);

        if (request is null)
        {
            return TypedResults.BadRequest();
        }

        var rubies = await ProcessPurchase(accountId, request.ItemId, request.ExpectedPurchasePrice, cancellationToken);

        if (rubies is not { } rubiesVal)
        {
            return TypedResults.BadRequest();
        }

        return EarthJson(rubiesVal.Purchased + rubiesVal.Earned);
    }

    [HttpPost("purchaseV2")]
    public async Task<Results<ContentHttpResult, BadRequest>> PurchaseV2(CancellationToken cancellationToken)
    {
        if (!TryGetAccountId(out var accountId))
        {
            return TypedResults.BadRequest();
        }

        var request = await Request.Body.AsJsonAsync<PurchaseItemRequest>(cancellationToken);

        if (request is null)
        {
            return TypedResults.BadRequest();
        }

        var rubies = await ProcessPurchase(accountId, request.ItemId, request.ExpectedPurchasePrice, cancellationToken);

        if (rubies is not { } rubiesVal)
        {
            return TypedResults.BadRequest();
        }

        return EarthJson(new Types.Profile.SplitRubies(rubiesVal.Purchased, rubiesVal.Earned));
    }

    private async Task<(int Purchased, int Earned)?> ProcessPurchase(Guid accountId, Guid itemId, int expectedPurchasePrice, CancellationToken cancellationToken)
    {
        if (!_staticData.Playfab.Items.TryGetValue(itemId, out var itemToPurchase))
        {
            Log.Debug($"Player {accountId} tried to purchase unknown item '{itemId}' (playfab)");
            return null;
        }

        int? playfabPrice = itemToPurchase.Data switch
        {
            Playfab.Item.BuildplateData data => data.Cost,
            Playfab.Item.InventoryItemData data => data.Cost,
            _ => null,
        };

        if (playfabPrice is not { } actualPurchasePrice)
        {
            return null;
        }

        // TODO: do this or just use actualPurchasePrice?
        if (expectedPurchasePrice != actualPurchasePrice)
        {
            return null;
        }

        var importer = new Importer(_earthDB, _eventBus, _objectStore, Log.Logger);

        Rubies? rubies = null;

        switch (itemToPurchase.Data)
        {
            case Playfab.Item.BuildplateData data:
                {
                    using var transaction = await _earthDB.Database.BeginTransactionAsync(cancellationToken);

                    try
                    {
                        var profile = await _earthDB.Profiles
                            .AsTracking()
                            .FirstOrNewAsync(profile => profile.Id == accountId, cancellationToken: cancellationToken);

                        if (profile.Rubies.Total < expectedPurchasePrice)
                        {
                            Log.Debug($"Player {accountId} tried to purchase item '{itemId}' but does not have enough rubies");
                            break;
                        }

                        var buidplateId = await importer.AddBuidplateToPlayer(data.Id, accountId, cancellationToken);

                        if (buidplateId is null)
                        {
                            Log.Warning($"Failed to add buildplate {data.Id} to player {accountId}");
                            break;
                        }

                        bool spent = profile.Rubies.Spend(expectedPurchasePrice);
                        Debug.Assert(spent);

                        await _earthDB.SaveChangesAsync(cancellationToken);
                        await transaction.CommitAsync(cancellationToken);

                        rubies = profile.Rubies;
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "Buildplate Purchase failed.");
                        await transaction.RollbackAsync(cancellationToken);
                    }
                }

                break;
            case Playfab.Item.InventoryItemData data:
                {
                    using var transaction = await _earthDB.Database.BeginTransactionAsync(cancellationToken);

                    try
                    {
                        var profile = await _earthDB.Profiles
                            .AsTracking()
                            .FirstOrNewAsync(profile => profile.Id == accountId, cancellationToken: cancellationToken);

                        var journal = await _earthDB.Journals
                            .AsTracking()
                            .FirstOrNewAsync(journal => journal.Id == accountId, cancellationToken: cancellationToken);

                        var inventory = await _earthDB.Inventories
                            .AsTracking()
                            .FirstOrNewAsync(inventory => inventory.Id == accountId, cancellationToken: cancellationToken);

                        if (profile.Rubies.Total < expectedPurchasePrice)
                        {
                            Log.Debug($"Player {accountId} tried to purchase item '{itemId}' but does not have enough rubies");
                            break;
                        }

                        inventory.AddItems(data.Id.ToString(), data.Amount);
                        journal.AddCollectedItem(data.Id.ToString(), U.CurrentTimeMillis(), data.Amount);

                        // TODO: add to activity log?

                        bool spent = profile.Rubies.Spend(expectedPurchasePrice);
                        Debug.Assert(spent);

                        await _earthDB.SaveChangesAsync(cancellationToken);
                        await transaction.CommitAsync(cancellationToken);

                        rubies = profile.Rubies;
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "Buildplate Purchase failed.");
                        await transaction.RollbackAsync(cancellationToken);
                    }
                }

                break;

            default:
                Log.Warning($"Shop item '{itemId}' has unknown {nameof(Playfab.Item.ItemData)}");
                break;
        }

        if (rubies is null)
        {
            return null;
        }

        return (rubies.Purchased, rubies.Earned);
    }
}
