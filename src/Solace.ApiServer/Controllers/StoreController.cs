using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using System.Text;
using Solace.ApiServer.Types.Buildplates;
using Solace.ApiServer.Types.Store;
using Solace.BuildplateImporter;
using Solace.Common.Utils;
using Solace.Db.Earth;
using Solace.Db.Earth.Models.Player;
using Solace.ObjectStore.Client;
using Solace.StaticData;
using Solace.EventBus.Client;
using Microsoft.EntityFrameworkCore;
using Solace.ApiServer.Utils;
using Solace.Db.Playfab;
using Solace.Db.Playfab.Models.Items;

namespace Solace.ApiServer.Controllers;

[Authorize]
[ApiVersion("1.1")]
[Route("1/api/v{version:apiVersion}/commerce")]
internal sealed partial class StoreController : SolaceControllerBase
{
    private readonly EarthDbContext _earthDb;
    private readonly PlayfabDbContext _playfabDb;
    private readonly EventBusClient _eventBus;
    private readonly ObjectStoreClient _objectStore;
    private readonly ILogger<StoreController> _logger;

    public StoreController(EarthDbContext earthDb, PlayfabDbContext playfabDb, EventBusClient eventBus, ObjectStoreClient objectStore, ILogger<StoreController> logger)
    {
        _earthDb = earthDb;
        _playfabDb = playfabDb;
        _eventBus = eventBus;
        _objectStore = objectStore;
        _logger = logger;
    }

    internal sealed record StoreItemInfoRequest(string Id, string StoreItemType, uint StreamVersion);

    [HttpPost("storeItemInfo")]
    public async Task<ContentHttpResult> GetStoreItemInfo(CancellationToken cancellationToken)
    {
        var request = await Request.Body.AsJsonAsync(AppJsonContext.Default.StoreItemInfoRequestArray, cancellationToken);

        if (request is null or { Length: 0 })
        {
            return EarthJson(Array.Empty<StoreItemInfo>());
        }

        List<StoreItemInfo> result = [with(request.Length)];

        foreach (var item in request)
        {
            switch (item.StoreItemType)
            {
                case "Buildplates":
                    {
                        var itemId = Guid.Parse(item.Id);

                        var buildplate = await _earthDb.TemplateBuildplates
                            .AsNoTracking()
                            .FirstOrDefaultAsync(template => template.Id == itemId, cancellationToken);

                        var storeItemType = Enum.Parse<StoreItemInfo.StoreItemTypeE>(item.StoreItemType);

                        if (buildplate is null)
                        {
                            LogBuildplateNotFound(item.Id);
                            result.Add(new StoreItemInfo(itemId, storeItemType, StoreItemInfo.StoreItemStatus.NotFound, item.StreamVersion, null, null, null, null, null));
                            break;
                        }

                        using var previewData = await _objectStore.GetStreamAsync(buildplate.PreviewObjectId, cancellationToken);

                        if (previewData is null)
                        {
                            LogBuildplatePreviewGetError(item.Id);
                            result.Add(new StoreItemInfo(itemId, storeItemType, StoreItemInfo.StoreItemStatus.NotFound, item.StreamVersion, null, null, null, null, null));
                            break;
                        }

                        using var previewDataReader = new StreamReader(previewData, Encoding.ASCII);

                        var model = await previewDataReader.ReadToEndAsync(cancellationToken);

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

    internal sealed record PurchaseItemRequest(
        int ExpectedPurchasePrice,
        Guid ItemId
    );

    [HttpPost("purchase")]
    public async Task<Results<ContentHttpResult, BadRequest>> Purchase(CancellationToken cancellationToken)
    {
        if (!TryGetProfileId(out var accountId))
        {
            return TypedResults.BadRequest();
        }

        var request = await Request.Body.AsJsonAsync(AppJsonContext.Default.PurchaseItemRequest, cancellationToken);

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
        if (!TryGetProfileId(out var accountId))
        {
            return TypedResults.BadRequest();
        }

        var request = await Request.Body.AsJsonAsync(AppJsonContext.Default.PurchaseItemRequest, cancellationToken);

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
        var itemToPurchase = await _playfabDb.Items
            .AsNoTracking()
            .Include(item => item.Data)
            .FirstOrDefaultAsync(item => item.Id == itemId, cancellationToken);

        if (itemToPurchase is null)
        {
            LogPurchaseUnknownItem(accountId, itemId);
            return null;
        }

        if (!itemToPurchase.Purchasable || itemToPurchase.StartDate > DateTimeOffset.UtcNow)
        {
            return null;
        }

        int? playfabPrice = itemToPurchase.Data switch
        {
            BuildplateDataEF data => data.Cost,
            InventoryItemDataEF data => data.Cost,
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

        await using var importer = new Importer(_earthDb, _eventBus, _objectStore, _logger)
        {
            OwnsEarthDb = false,
            OwnsEventBusClient = false,
            OwnsObjectStoreClient = false,
        };

        Rubies? rubies = null;

        switch (itemToPurchase.Data)
        {
            case BuildplateDataEF data:
                {
                    await using var transaction = await _earthDb.Database.BeginTransactionAsync(cancellationToken);

                    try
                    {
                        var profile = await _earthDb.Profiles
                            .AsTracking()
                            .FirstAsync(profile => profile.Id == accountId, cancellationToken: cancellationToken);

                        if (profile.Rubies.Total < expectedPurchasePrice)
                        {
                            LogPurchaseInsufficientRubies(accountId, itemId);
                            break;
                        }

                        var buidplateId = await importer.AddBuidplateToPlayer(data.BuildplateId, accountId, cancellationToken);

                        if (buidplateId is null)
                        {
                            LogBuildplateAddFail(accountId, data.BuildplateId);
                            break;
                        }

                        var spent = profile.Rubies.Spend(expectedPurchasePrice);
                        Debug.Assert(spent);

                        await _earthDb.SaveChangesAsync(cancellationToken);
                        await transaction.CommitAsync(cancellationToken);

                        rubies = profile.Rubies;
                    }
                    catch (Exception exception)
                    {
                        LogPurchaseFailed(exception, accountId, "Buildplate");
                        await transaction.RollbackAsync(cancellationToken);
                    }
                }

                break;
            case InventoryItemDataEF data:
                {
                    await using var transaction = await _earthDb.Database.BeginTransactionAsync(cancellationToken);

                    try
                    {
                        var profile = await _earthDb.Profiles
                            .AsTracking()
                            .FirstAsync(profile => profile.Id == accountId, cancellationToken: cancellationToken);

                        if (profile.Rubies.Total < expectedPurchasePrice)
                        {
                            LogPurchaseInsufficientRubies(accountId, itemId);
                            break;
                        }

                        await InventoryUtils.AddStackableItemsAsync(_earthDb, ResultsEF.Builder.Null, accountId, data.ItemId, data.Amount, cancellationToken);
                        await JournalUtils.AddCollectedItemAsync(_earthDb, ResultsEF.Builder.Null, accountId, data.ItemId, DateTimeOffset.UtcNow, data.Amount, cancellationToken);

                        // TODO: add to activity log?

                        var spent = profile.Rubies.Spend(expectedPurchasePrice);
                        Debug.Assert(spent);

                        await _earthDb.SaveChangesAsync(cancellationToken);
                        await transaction.CommitAsync(cancellationToken);

                        rubies = profile.Rubies;
                    }
                    catch (Exception exception)
                    {
                        LogPurchaseFailed(exception, accountId, "Item");
                        await transaction.RollbackAsync(cancellationToken);
                    }
                }

                break;

            default:
                throw new UnreachableException($"Shop item '{itemId}' has unknown {nameof(Playfab.Item.ItemData)}");
        }

        if (rubies is null)
        {
            return null;
        }

        return (rubies.Purchased, rubies.Earned);
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Buildplate with id {BuildplateId} not found")]
    private partial void LogBuildplateNotFound(string BuildplateId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to get preview for buildplate {BuildplateId}")]
    private partial void LogBuildplatePreviewGetError(string BuildplateId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Player '{AccountId}' tried to purchase unknown item '{ItemId}' (playfab)")]
    private partial void LogPurchaseUnknownItem(Guid AccountId, Guid ItemId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Player {AccountId} tried to purchase item '{ItemId}' but does not have enough rubies")]
    private partial void LogPurchaseInsufficientRubies(Guid AccountId, Guid ItemId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to add buildplate {BuildplateId} to player {AccountId}")]
    private partial void LogBuildplateAddFail(Guid AccountId, Guid BuildplateId);

    [LoggerMessage(Level = LogLevel.Error, Message = "{PurchaseType} purchase failed for account '{AccountId}'")]
    private partial void LogPurchaseFailed(Exception exception, Guid AccountId, string PurchaseType);
}
