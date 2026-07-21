using System.Diagnostics;
using System.Text;
using Solace.Buildplate.Connector.Model;
using Solace.Common;
using Solace.Common.Utils;
using Solace.Db.Earth;
using Solace.Db.Earth.Models.Player;
using Solace.EventBus.Client;
using Solace.ObjectStore.Client;
using Solace.StaticData;
using CICIBIEType = Solace.StaticData.Catalog.ItemsCatalogR.Item.BoostEffectType;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace Solace.ApiServer.Utils;

internal sealed partial class BuildplateInstanceRequestHandler : IAsyncDisposable
{
    private readonly IDbContextFactory<EarthDbContext> _earthDbFactory;
    private readonly ObjectStoreClient _objectStoreClient;
    private readonly Catalog _catalog;
    private readonly BuildplateInstancesManager _buildplateInstancesManager;

    private readonly ILogger<BuildplateInstanceRequestHandler> _logger;

    private RequestHandler? _requestHandler;

    public BuildplateInstanceRequestHandler(IDbContextFactory<EarthDbContext> earthDbFactory, ObjectStoreClient objectStoreClient, StaticData.StaticDataProvider staticData, BuildplateInstancesManager buildplateInstancesManager, ILogger<BuildplateInstanceRequestHandler> logger)
    {
        _earthDbFactory = earthDbFactory;
        _objectStoreClient = objectStoreClient;
        _catalog = staticData.Catalog;
        _buildplateInstancesManager = buildplateInstancesManager;
        _logger = logger;
    }

    internal async Task InitializeAsync(EventBusClient eventBusClient)
        => _requestHandler = await eventBusClient.AddRequestHandlerAsync("buildplates",
            async request =>
            {
                try
                {
                    switch (request.Type)
                    {
                        case "load":
                            {
                                BuildplateLoadRequest? buildplateLoadRequest = ReadRawRequest<BuildplateLoadRequest>(request.Data, _logger);
                                if (buildplateLoadRequest is null)
                                {
                                    return null;
                                }

                                BuildplateLoadResponse? buildplateLoadResponse = await HandleLoadAsync(buildplateLoadRequest.PlayerId, buildplateLoadRequest.BuildplateId);
                                return buildplateLoadResponse is not null ? Json.Serialize(buildplateLoadResponse) : null;
                            }
                        case "loadShared":
                            {
                                SharedBuildplateLoadRequest? sharedBuildplateLoadRequest = ReadRawRequest<SharedBuildplateLoadRequest>(request.Data, _logger);
                                if (sharedBuildplateLoadRequest is null)
                                {
                                    return null;
                                }

                                BuildplateLoadResponse? buildplateLoadResponse = await HandleLoadSharedAsync(sharedBuildplateLoadRequest.SharedBuildplateId);
                                return buildplateLoadResponse is not null ? Json.Serialize(buildplateLoadResponse) : null;
                            }
                        case "loadEncounter":

                            {
                                EncounterBuildplateLoadRequest? encounterBuildplateLoadRequest = ReadRawRequest<EncounterBuildplateLoadRequest>(request.Data, _logger);
                                if (encounterBuildplateLoadRequest is null)
                                {
                                    return null;
                                }

                                BuildplateLoadResponse? buildplateLoadResponse = await HandleLoadEncounterAsync(encounterBuildplateLoadRequest.EncounterBuildplateId);
                                return buildplateLoadResponse is not null ? Json.Serialize(buildplateLoadResponse) : null;
                            }
                        case "saved":
                            {
                                RequestWithInstanceId<WorldSavedMessage>? requestWithInstanceId = ReadRequest<WorldSavedMessage>(request.Data, _logger);
                                return requestWithInstanceId is null
                                    ? null
                                    : await HandleSavedAsync(requestWithInstanceId.InstanceId, requestWithInstanceId.Request.DataBase64, request.Timestamp) ? "" : null;
                            }
                        case "playerConnected":
                            {
                                // Log.Debug("RequestHandler playerConnected");
                                RequestWithInstanceId<PlayerConnectedRequest>? requestWithInstanceId = ReadRequest<PlayerConnectedRequest>(request.Data, _logger);
                                if (requestWithInstanceId is null)
                                {
                                    return null;
                                }

                                PlayerConnectedResponse? playerConnectedResponse = await HandlePlayerConnectedAsync(requestWithInstanceId.InstanceId, requestWithInstanceId.Request);
                                return playerConnectedResponse is not null ? Json.Serialize(playerConnectedResponse) : null;
                            }
                        case "playerDisconnected":
                            {
                                RequestWithInstanceId<PlayerDisconnectedRequest>? requestWithInstanceId = ReadRequest<PlayerDisconnectedRequest>(request.Data, _logger);
                                if (requestWithInstanceId is null)
                                {
                                    return null;
                                }

                                PlayerDisconnectedResponse? playerDisconnectedResponse = await HandlePlayerDisconnectedAsync(requestWithInstanceId.InstanceId, requestWithInstanceId.Request, request.Timestamp);
                                return playerDisconnectedResponse is not null ? Json.Serialize(playerDisconnectedResponse) : null;
                            }
                        case "playerDead":
                            {
                                RequestWithInstanceId<Guid>? requestWithInstanceId = ReadRequest<Guid>(request.Data, _logger);
                                if (requestWithInstanceId is null)
                                {
                                    return null;
                                }

                                var respawn = HandlePlayerDead(requestWithInstanceId.InstanceId, requestWithInstanceId.Request, request.Timestamp);
                                return respawn is not null ? Json.Serialize(respawn.Value) : null;
                            }
                        case "getInitialPlayerState":
                            {
                                RequestWithInstanceId<Guid>? requestWithInstanceId = ReadRequest<Guid>(request.Data, _logger);
                                if (requestWithInstanceId is null)
                                {
                                    return null;
                                }

                                InitialPlayerStateResponse? initialPlayerStateResponse = await HandleGetInitialPlayerStateAsync(requestWithInstanceId.InstanceId, requestWithInstanceId.Request, request.Timestamp);
                                return initialPlayerStateResponse is not null ? Json.Serialize(initialPlayerStateResponse) : null;
                            }
                        case "getInventory":
                            {
                                RequestWithInstanceId<Guid>? requestWithInstanceId = ReadRequest<Guid>(request.Data, _logger);
                                if (requestWithInstanceId is null)
                                {
                                    return null;
                                }

                                InventoryResponse? inventoryResponse = await HandleGetInventoryAsync(requestWithInstanceId.InstanceId, requestWithInstanceId.Request);
                                return inventoryResponse is not null ? Json.Serialize(inventoryResponse) : null;
                            }
                        case "inventoryAdd":
                            {
                                RequestWithInstanceId<InventoryAddItemMessage>? requestWithInstanceId = ReadRequest<InventoryAddItemMessage>(request.Data, _logger);
                                return requestWithInstanceId is null
                                    ? null
                                    : await HandleInventoryAddAsync(requestWithInstanceId.InstanceId, requestWithInstanceId.Request, request.Timestamp) ? "" : null;
                            }
                        case "inventoryRemove":
                            {
                                RequestWithInstanceId<InventoryRemoveItemRequest>? requestWithBuildplateId = ReadRequest<InventoryRemoveItemRequest>(request.Data, _logger);
                                if (requestWithBuildplateId is null)
                                {
                                    return null;
                                }

                                var response = await HandleInventoryRemoveAsync(requestWithBuildplateId.InstanceId, requestWithBuildplateId.Request);
                                return response is not null ? Json.Serialize(response) : null;
                            }
                        case "inventoryUpdateWear":
                            {
                                RequestWithInstanceId<InventoryUpdateItemWearMessage>? requestWithInstanceId = ReadRequest<InventoryUpdateItemWearMessage>(request.Data, _logger);

                                return requestWithInstanceId is null
                                    ? null
                                    : await HandleInventoryUpdateWearAsync(requestWithInstanceId.InstanceId, requestWithInstanceId.Request) ? "" : null;
                            }
                        case "inventorySetHotbar":
                            {
                                RequestWithInstanceId<InventorySetHotbarMessage>? requestWithInstanceId = ReadRequest<InventorySetHotbarMessage>(request.Data, _logger);

                                return requestWithInstanceId is null
                                    ? null
                                    : await HandleInventorySetHotbarAsync(requestWithInstanceId.InstanceId, requestWithInstanceId.Request) ? "" : null;
                            }
                        default:
                            return null;
                    }
                }
                catch (Exception exception) when (exception is DbUpdateException or DbUpdateConcurrencyException)
                {
                    LogDatabaseErrorWhileHandlingRequest(exception);
                    return null;
                }
            },
            async exception =>
            {
                LogBuildplatesEventBusRequestHandlerError(exception);
                Console.Error.WriteLine(exception);
                Console.Error.Flush();
                Environment.Exit(1);
            }
        );

    public async ValueTask DisposeAsync()
    {
        if (_requestHandler is not null)
        {
            await _requestHandler.DisposeAsync();
        }
    }

    private sealed record BuildplateLoadRequest(
        Guid PlayerId,
        Guid BuildplateId
    );

    private sealed record SharedBuildplateLoadRequest(
        Guid SharedBuildplateId
    );

    private sealed record EncounterBuildplateLoadRequest(
        Guid EncounterBuildplateId
    );

    private sealed record BuildplateLoadResponse(
        string ServerDataBase64
    );

    private async Task<BuildplateLoadResponse?> HandleLoadAsync(Guid accountId, Guid buildplateId, CancellationToken cancellationToken = default)
    {
        await using var earthDb = await _earthDbFactory.CreateDbContextAsync(cancellationToken);

        var buildplate = await earthDb.PlayerBuildplates
            .AsNoTracking()
            .FirstOrDefaultAsync(buildplate => buildplate.Id == buildplateId && buildplate.AccountId == accountId, cancellationToken: cancellationToken);

        if (buildplate is null)
        {
            return null;
        }

        var serverData = await _objectStoreClient.GetMemoryAsync(buildplate.ServerDataObjectId, cancellationToken);
        if (serverData is null)
        {
            LogFailedToGetServerData(buildplate.ServerDataObjectId, buildplateId);
            return null;
        }

        var serverDataBase64 = Convert.ToBase64String(serverData.Value.Span);

        return new BuildplateLoadResponse(serverDataBase64);
    }

    private async Task<BuildplateLoadResponse?> HandleLoadSharedAsync(Guid sharedBuildplateId, CancellationToken cancellationToken = default)
    {
        await using var earthDb = await _earthDbFactory.CreateDbContextAsync(cancellationToken);

        var sharedBuildplate = await earthDb.SharedBuildplates
            .AsNoTracking()
            .FirstOrDefaultAsync(sharedBuildplate => sharedBuildplate.Id == sharedBuildplateId, cancellationToken: cancellationToken);

        if (sharedBuildplate is null)
        {
            return null;
        }

        var serverData = await _objectStoreClient.GetMemoryAsync(sharedBuildplate.ServerDataObjectId, cancellationToken);
        if (serverData is null)
        {
            LogFailedToGetServerDataShared(sharedBuildplate.ServerDataObjectId, sharedBuildplateId);
            return null;
        }

        var serverDataBase64 = Convert.ToBase64String(serverData.Value.Span);

        return new BuildplateLoadResponse(serverDataBase64);
    }

    private async Task<BuildplateLoadResponse?> HandleLoadEncounterAsync(Guid encounterBuildplateId, CancellationToken cancellationToken = default)
    {
        await using var earthDb = await _earthDbFactory.CreateDbContextAsync(cancellationToken);

        var encounterBuildplate = await earthDb.EncounterBuildplates
            .AsNoTracking()
            .FirstOrDefaultAsync(encounterBuildplate => encounterBuildplate.Id == encounterBuildplateId, cancellationToken);

        if (encounterBuildplate is null)
        {
            return null;
        }

        var serverData = await _objectStoreClient.GetMemoryAsync(encounterBuildplate.ServerDataObjectId, cancellationToken);
        if (serverData is null)
        {
            LogFailedToGetServerDataEncounter(encounterBuildplate.ServerDataObjectId, encounterBuildplateId);
            return null;
        }

        var serverDataBase64 = Convert.ToBase64String(serverData.Value.Span);

        return new BuildplateLoadResponse(serverDataBase64);
    }

    private async Task<bool> HandleSavedAsync(Guid instanceId, string dataBase64, DateTimeOffset timestamp, CancellationToken cancellationToken = default)
    {
        BuildplateInstancesManager.InstanceInfo? instanceInfo = _buildplateInstancesManager.GetInstanceInfo(instanceId);
        if (instanceInfo is null)
        {
            return false;
        }

        if (instanceInfo.Type != BuildplateInstancesManager.InstanceType.BUILD)
        {
            return false;
        }

        var accountId = instanceInfo.PlayerId;
        var buildplateId = instanceInfo.BuildplateId;

        Debug.Assert(accountId is not null);

        byte[] serverData;
        try
        {
            serverData = Convert.FromBase64String(dataBase64);
        }
        catch
        {
            return false;
        }

        await using var earthDb = await _earthDbFactory.CreateDbContextAsync(cancellationToken);

        var buildplateUnsafeForPreviewGenerator = await earthDb.PlayerBuildplates
            .AsNoTracking()
            .FirstOrDefaultAsync(buildplate => buildplate.Id == buildplateId, cancellationToken);

        if (buildplateUnsafeForPreviewGenerator is null)
        {
            return false;
        }

        var preview = await _buildplateInstancesManager.GetBuildplatePreviewAsync(serverData, buildplateUnsafeForPreviewGenerator.Night);
        if (preview is null)
        {
            LogCouldNotGeneratePreviewForBuildplate();
        }

        var serverDataObjectId = await _objectStoreClient.StoreAsync(serverData, cancellationToken);
        if (serverDataObjectId is null)
        {
            LogFailedToStoreData(buildplateId);
            return false;
        }

        Guid? previewObjectId;
        if (preview is not null)
        {
            previewObjectId = await _objectStoreClient.StoreAsync(Encoding.ASCII.GetBytes(preview), cancellationToken);
            if (previewObjectId is null)
            {
                LogFailedToStorePreview(buildplateId);
            }
        }
        else
        {
            previewObjectId = null;
        }

        try
        {
            var buildplate = await earthDb.PlayerBuildplates
                .AsTracking()
                .FirstOrDefaultAsync(buildplate => buildplate.Id == buildplateId && buildplate.AccountId == accountId, cancellationToken);

            if (buildplate is null)
            {
                await _objectStoreClient.DeleteAsync(serverDataObjectId.Value, cancellationToken);
                if (previewObjectId is not null)
                {
                    await _objectStoreClient.DeleteAsync(previewObjectId.Value, cancellationToken);
                }

                return false;
            }

            var oldServerDataObjectId = buildplate.ServerDataObjectId;

            buildplate.LastModified = timestamp;
            buildplate.ServerDataObjectId = serverDataObjectId.Value;

            Guid? oldPreviewObjectId;
            if (previewObjectId is not null)
            {
                oldPreviewObjectId = buildplate.PreviewObjectId;
                buildplate.PreviewObjectId = previewObjectId.Value;
            }
            else
            {
                oldPreviewObjectId = null;
            }

            await earthDb.SaveChangesAsync(cancellationToken);

            await _objectStoreClient.DeleteAsync(oldServerDataObjectId, cancellationToken);

            if (!Guid.IsNullOrZero(oldPreviewObjectId))
            {
                await _objectStoreClient.DeleteAsync(oldPreviewObjectId.Value, cancellationToken);
            }

            LogStoredNewSnapshotForBuildplate(buildplateId);

            return true;
        }
        catch (Exception exception) when (exception is DbUpdateException or DbUpdateConcurrencyException)
        {
            LogErrorSavingWorld(exception);

            await _objectStoreClient.DeleteAsync(serverDataObjectId.Value, cancellationToken);
            if (previewObjectId is not null)
            {
                await _objectStoreClient.DeleteAsync(previewObjectId.Value, cancellationToken);
            }

            throw;
        }
    }

    private async Task<PlayerConnectedResponse?> HandlePlayerConnectedAsync(Guid instanceId, PlayerConnectedRequest playerConnectedRequest, CancellationToken cancellationToken = default)
    {
        // TODO: check join code etc.

        BuildplateInstancesManager.InstanceInfo? instanceInfo = _buildplateInstancesManager.GetInstanceInfo(instanceId);

        if (instanceInfo is null)
        {
            return null;
        }

        await using var earthDb = await _earthDbFactory.CreateDbContextAsync(cancellationToken);

        InventoryResponse? initialInventoryContents;
        switch (instanceInfo.Type)
        {
            case BuildplateInstancesManager.InstanceType.BUILD:
                {
                    initialInventoryContents = null;
                }

                break;
            case BuildplateInstancesManager.InstanceType.PLAY:
                {
                    var stackableItems = earthDb.StackableItems
                        .AsNoTracking()
                        .Where(item => item.AccountId == playerConnectedRequest.Uuid)
                        .AsAsyncEnumerable();

                    var nonStackableItems = earthDb.NonStackableItems
                        .AsNoTracking()
                        .Where(item => item.AccountId == playerConnectedRequest.Uuid)
                        .AsAsyncEnumerable();

                    var hotbar = await earthDb.Hotbars
                        .AsNoTracking()
                        .FirstAsync(hotbar => hotbar.Id == playerConnectedRequest.Uuid, cancellationToken: cancellationToken);

                    initialInventoryContents = new InventoryResponse(
                        await AsyncEnumerable.Concat(
                            stackableItems
                                .Select(item => new InventoryResponse.Item(item.ItemId, item.Count, null, 0)),
                            nonStackableItems
                                .Select(instance => new InventoryResponse.Item(instance.ItemId, 1, instance.InstanceId, instance.Wear))
                        ).Where(item => item.Count > 0).ToArrayAsync(cancellationToken),
                        [.. hotbar.Items.Select(item => item is { Count: > 0 } ? new InventoryResponse.HotbarItem(item.Uuid, item.Count, item.InstanceId) : null)]
                    );
                }

                break;
            case BuildplateInstancesManager.InstanceType.SHARED_BUILD or BuildplateInstancesManager.InstanceType.SHARED_PLAY:
                {
                    var sharedBuildplate = await earthDb.SharedBuildplates
                        .AsNoTracking()
                        .FirstOrDefaultAsync(sharedBuildplate => sharedBuildplate.Id == instanceInfo.BuildplateId, cancellationToken: cancellationToken);

                    if (sharedBuildplate is null)
                    {
                        return null;
                    }

                    initialInventoryContents = new InventoryResponse(
                        [.. Enumerable.Concat(
                            sharedBuildplate.Hotbar
                                .Where(item => item is { Count: > 0, InstanceId: null })
                                .GroupBy(item => item!.Uuid)
                                .ToDictionary(
                                    group => group.Key,
                                    group => group.Sum(item => item!.Count)
                                )
                                .Select(entry => new InventoryResponse.Item(entry.Key, entry.Value, null, 0)),
                            sharedBuildplate.Hotbar
                                .Where(item => item is { Count: > 0, InstanceId: not null })
                                .Select(item => new InventoryResponse.Item(item!.Uuid, 1, item.InstanceId, item.Wear))
                        )],
                        [.. sharedBuildplate.Hotbar.Select(item => item is { Count: > 0 } ? new InventoryResponse.HotbarItem(item.Uuid, item.Count, item.InstanceId) : null)]
                    );
                }

                break;
            case BuildplateInstancesManager.InstanceType.ENCOUNTER:
                {
                    var hotbar = await earthDb.Hotbars
                        .AsTracking()
                        .FirstAsync(hotbar => hotbar.Id == playerConnectedRequest.Uuid, cancellationToken: cancellationToken);

                    var inventoryResponseHotbar = new InventoryResponse.HotbarItem[7];
                    Dictionary<Guid, int> inventoryResponseStackableItems = [];
                    LinkedList<InventoryResponse.Item> inventoryResponseNonStackableItems = [];
                    for (var index = 0; index < 7; index++)
                    {
                        var item = hotbar.Items[index];
                        if (item is not null)
                        {
                            if (item.InstanceId is null)
                            {
                                await InventoryUtils.TakeStackableItemsAsync(earthDb, ResultsEF.Builder.Null, playerConnectedRequest.Uuid, item.Uuid, item.Count, cancellationToken);
                                inventoryResponseStackableItems[item.Uuid] = inventoryResponseStackableItems.GetValueOrDefault(item.Uuid, 0) + item.Count;
                                inventoryResponseHotbar[index] = new InventoryResponse.HotbarItem(item.Uuid, item.Count, null);
                            }
                            else
                            {
                                var wear = (await InventoryUtils.TakeInstanceItemsAsync(earthDb, ResultsEF.Builder.Null, playerConnectedRequest.Uuid, item.Uuid, [item.InstanceId.Value], cancellationToken)).First().Wear;
                                inventoryResponseNonStackableItems.AddLast(new InventoryResponse.Item(item.Uuid, 1, item.InstanceId, wear));
                                inventoryResponseHotbar[index] = new InventoryResponse.HotbarItem(item.Uuid, 1, item.InstanceId);
                            }
                        }
                    }

                    await HotbarUtils.LimitToInventoryAsync(earthDb, playerConnectedRequest.Uuid, hotbar, cancellationToken);

                    initialInventoryContents = new InventoryResponse(
                        [
                            .. inventoryResponseStackableItems.Select(entry => new InventoryResponse.Item(entry.Key, entry.Value, null, 0)),
                            .. inventoryResponseNonStackableItems
                        ],
                        inventoryResponseHotbar
                    );

                    await earthDb.SaveChangesAsync(cancellationToken);
                }

                break;
            default:
                {
                    // shouldn't happen, safe default
                    LogExpectedBackpackContentsInPlayerDisconnectedRequest(instanceInfo.Type);
                    initialInventoryContents = new InventoryResponse([], new InventoryResponse.HotbarItem[7]);
                }

                break;
        }

        var playerConnectedResponse = new PlayerConnectedResponse(
            true,
            initialInventoryContents
        );

        return playerConnectedResponse;
    }

    private async Task<PlayerDisconnectedResponse?> HandlePlayerDisconnectedAsync(Guid instanceId, PlayerDisconnectedRequest playerDisconnectedRequest, DateTimeOffset timestamp, CancellationToken cancellationToken = default)
    {
        BuildplateInstancesManager.InstanceInfo? instanceInfo = _buildplateInstancesManager.GetInstanceInfo(instanceId);
        if (instanceInfo is null)
        {
            return null;
        }

        var usesBackpack = instanceInfo.Type is BuildplateInstancesManager.InstanceType.ENCOUNTER;
        if (usesBackpack)
        {
            await using var earthDb = await _earthDbFactory.CreateDbContextAsync(cancellationToken);

            InventoryResponse? backpackContents = playerDisconnectedRequest.BackpackContents;
            if (backpackContents is null)
            {
                LogExpectedBackpackContentsInPlayerDisconnectedRequest();
                return null;
            }

            var hotbar = await earthDb.Hotbars
                .AsTracking()
                .FirstAsync(hotbar => hotbar.Id == playerDisconnectedRequest.PlayerId, cancellationToken: cancellationToken);

            LinkedList<Guid> unlockedJournalItems = [];
            foreach (var item in backpackContents.Items)
            {
                var catalogItem = _catalog.ItemsCatalog.GetItem(item.Id);
                if (catalogItem is null)
                {
                    LogBackpackContentsContainedItemThatIsNotInItemCatalog();
                    continue;
                }

                if (!catalogItem.Stackable && item.InstanceId is null)
                {
                    LogBackpackContentsContainedNonStackableItemWithoutInstanceId();
                    continue;
                }

                if (catalogItem.Stackable)
                {
                    await InventoryUtils.AddStackableItemsAsync(earthDb, ResultsEF.Builder.Null, playerDisconnectedRequest.PlayerId, item.Id, item.Count, cancellationToken);
                }
                else
                {
                    Debug.Assert(item.InstanceId is not null);

                    await InventoryUtils.AddInstanceItemsAsync(earthDb, ResultsEF.Builder.Null, [new NonStackableItemInstanceEF(playerDisconnectedRequest.PlayerId, item.Id, item.InstanceId.Value, item.Wear)], cancellationToken);
                }

                if (await JournalUtils.AddCollectedItemAsync(earthDb, ResultsEF.Builder.Null, playerDisconnectedRequest.PlayerId, item.Id, timestamp, item.Count, cancellationToken) is 0)
                {
                    if (catalogItem.JournalEntry is not null)
                    {
                        unlockedJournalItems.AddLast(item.Id);
                    }
                }
            }

            for (var index = 0; index < 7; index++)
            {
                InventoryResponse.HotbarItem? hotbarItem = backpackContents.Hotbar[index];
                if (hotbarItem is not null)
                {
                    hotbar.Items[index] = new HotbarEF.Item(hotbarItem.Id, hotbarItem.Count, hotbarItem.InstanceId);
                }
            }

            await HotbarUtils.LimitToInventoryAsync(earthDb, playerDisconnectedRequest.PlayerId, hotbar, cancellationToken);

            await earthDb.SaveChangesAsync(cancellationToken);

            foreach (var itemId in unlockedJournalItems)
            {
                await TokenUtils.AddTokenAsync(earthDb, ResultsEF.Builder.Null, new JournalItemUnlockedTokenEF(playerDisconnectedRequest.PlayerId, itemId), cancellationToken);
            }
        }

        return new PlayerDisconnectedResponse();
    }

    private bool? HandlePlayerDead(Guid instanceId, Guid playerId, DateTimeOffset currentTime)
    {
        _ = playerId;
        _ = currentTime;

        var instanceInfo = _buildplateInstancesManager.GetInstanceInfo(instanceId);
        return instanceInfo is null
            ? null
            : instanceInfo.Type is BuildplateInstancesManager.InstanceType.BUILD or BuildplateInstancesManager.InstanceType.SHARED_BUILD;
    }

    private sealed record EffectInfo(
        DateTimeOffset EndTime,
        Catalog.ItemsCatalogR.Item.BoostEffect Effect
    );

    private async Task<InitialPlayerStateResponse?> HandleGetInitialPlayerStateAsync(Guid instanceId, Guid accountId, DateTimeOffset currentTime, CancellationToken cancellationToken = default)
    {
        BuildplateInstancesManager.InstanceInfo? instanceInfo = _buildplateInstancesManager.GetInstanceInfo(instanceId);

        if (instanceInfo is null)
        {
            return null;
        }

        var (useHealth, useBoosts) = instanceInfo.Type switch
        {
            BuildplateInstancesManager.InstanceType.BUILD => (false, false),
            BuildplateInstancesManager.InstanceType.PLAY => (false, true),
            BuildplateInstancesManager.InstanceType.SHARED_BUILD => (false, false),
            BuildplateInstancesManager.InstanceType.SHARED_PLAY => (false, true),
            BuildplateInstancesManager.InstanceType.ENCOUNTER => (true, true),
            _ => (false, false),
        };

        if (!useHealth && !useBoosts)
        {
            return new InitialPlayerStateResponse(20.0f, []);
        }
        else
        {
            if (!useBoosts)
            {
                throw new UnreachableException();
            }

            await using var earthDb = await _earthDbFactory.CreateDbContextAsync(cancellationToken);

            var profile = await earthDb.Profiles
                .AsNoTracking()
                .FirstAsync(profile => profile.Id == accountId, cancellationToken: cancellationToken);

            var boosts = await earthDb.Boosts
                .AsNoTracking()
                .FirstAsync(boosts => boosts.Id == accountId, cancellationToken: cancellationToken);

            float maxHealth = BoostUtils.GetMaxPlayerHealth(boosts, currentTime, _catalog.ItemsCatalog);

            return new InitialPlayerStateResponse(
                useHealth ? float.Min(profile.Health, maxHealth) : maxHealth,
                [.. boosts.ActiveBoosts
                .Where(activeBoost => activeBoost is not null)
                .Where(activeBoost => activeBoost!.StartTime + activeBoost.Duration >= currentTime)
                .SelectMany(activeBoost => _catalog.ItemsCatalog.GetItem(activeBoost!.ItemId)!.BoostInfo!.Effects.Select(effect => new EffectInfo(activeBoost.StartTime + activeBoost.Duration, effect)))
                .Where(effectInfo => effectInfo.Effect.Type is CICIBIEType.ADVENTURE_XP or CICIBIEType.DEFENSE or CICIBIEType.EATING or CICIBIEType.HEALTH or CICIBIEType.MINING_SPEED or CICIBIEType.STRENGTH)
                .Select(effectInfo => new InitialPlayerStateResponse.BoostStatusEffect(
                    effectInfo.Effect.Type switch
                    {
                        CICIBIEType.ADVENTURE_XP => InitialPlayerStateResponse.BoostStatusEffect.TypeE.ADVENTURE_XP,
                        CICIBIEType.DEFENSE => InitialPlayerStateResponse.BoostStatusEffect.TypeE.DEFENSE,
                        CICIBIEType.EATING => InitialPlayerStateResponse.BoostStatusEffect.TypeE.EATING,
                        CICIBIEType.HEALTH => InitialPlayerStateResponse.BoostStatusEffect.TypeE.HEALTH,
                        CICIBIEType.MINING_SPEED => InitialPlayerStateResponse.BoostStatusEffect.TypeE.MINING_SPEED,
                        CICIBIEType.STRENGTH => InitialPlayerStateResponse.BoostStatusEffect.TypeE.STRENGTH,
                        _ => throw new UnreachableException(),
                    },
                    effectInfo.Effect.Value,
                    effectInfo.EndTime - currentTime
                ))]
            );
        }
    }

#pragma warning disable IDE0060 // Remove unused parameter
    private async Task<InventoryResponse?> HandleGetInventoryAsync(Guid instanceId, Guid requestedInventoryAccountId, CancellationToken cancellationToken = default)
#pragma warning restore IDE0060 // Remove unused parameter
    {
        await using var earthDb = await _earthDbFactory.CreateDbContextAsync(cancellationToken);

        var stackableItems = earthDb.StackableItems
            .AsNoTracking()
            .Where(item => item.AccountId == requestedInventoryAccountId)
            .AsAsyncEnumerable();

        var nonStackableItems = earthDb.NonStackableItems
            .AsNoTracking()
            .Where(item => item.AccountId == requestedInventoryAccountId)
            .AsAsyncEnumerable();

        var hotbar = await earthDb.Hotbars
            .AsNoTracking()
            .FirstAsync(hotbar => hotbar.Id == requestedInventoryAccountId, cancellationToken: cancellationToken);

        return new InventoryResponse(
            await AsyncEnumerable.Concat(
                stackableItems
                    .Select(item => new InventoryResponse.Item(item.ItemId, item.Count, null, 0)),
                nonStackableItems
                    .Select(instance => new InventoryResponse.Item(instance.ItemId, 1, instance.InstanceId, instance.Wear))
            ).Where(item => item.Count > 0).ToArrayAsync(cancellationToken),
            [.. hotbar.Items.Select(item => item is { Count: > 0 } ? new InventoryResponse.HotbarItem(item.Uuid, item.Count, item.InstanceId) : null)]
        );
    }

#pragma warning disable IDE0060 // Remove unused parameter
    private async Task<bool> HandleInventoryAddAsync(Guid instanceId, InventoryAddItemMessage inventoryAddItemMessage, DateTimeOffset timestamp, CancellationToken cancellationToken = default)
#pragma warning restore IDE0060 // Remove unused parameter
    {
        Catalog.ItemsCatalogR.Item? catalogItem = _catalog.ItemsCatalog.GetItem(inventoryAddItemMessage.ItemId);
        if (catalogItem is null)
        {
            return false;
        }

        if (!catalogItem.Stackable && inventoryAddItemMessage.InstanceId is null)
        {
            return false;
        }

        await using var earthDb = await _earthDbFactory.CreateDbContextAsync(cancellationToken);

        if (catalogItem.Stackable)
        {
            await InventoryUtils.AddStackableItemsAsync(earthDb, ResultsEF.Builder.Null, inventoryAddItemMessage.PlayerId, inventoryAddItemMessage.ItemId, inventoryAddItemMessage.Count, cancellationToken);
        }
        else
        {
            await InventoryUtils.AddInstanceItemsAsync(earthDb, ResultsEF.Builder.Null, [new NonStackableItemInstanceEF(inventoryAddItemMessage.PlayerId, inventoryAddItemMessage.ItemId, inventoryAddItemMessage.InstanceId!.Value, inventoryAddItemMessage.Wear)], cancellationToken);
        }

        var journalItemUnlocked = false;
        if (await JournalUtils.AddCollectedItemAsync(earthDb, ResultsEF.Builder.Null, inventoryAddItemMessage.PlayerId, inventoryAddItemMessage.ItemId, timestamp, inventoryAddItemMessage.Count, cancellationToken) == 0)
        {
            if (catalogItem.JournalEntry is not null)
            {
                journalItemUnlocked = true;
            }
        }

        await earthDb.SaveChangesAsync(cancellationToken);

        if (journalItemUnlocked)
        {
            await TokenUtils.AddTokenAsync(earthDb, ResultsEF.Builder.Null, new JournalItemUnlockedTokenEF(inventoryAddItemMessage.PlayerId, inventoryAddItemMessage.ItemId), cancellationToken);
        }

        return true;
    }

    private async Task<object> HandleInventoryRemoveAsync(Guid instanceId, InventoryRemoveItemRequest inventoryRemoveItemRequest, CancellationToken cancellationToken = default)
    {
        await using var earthDb = await _earthDbFactory.CreateDbContextAsync(cancellationToken);

        var hotbar = await earthDb.Hotbars
            .AsTracking()
            .FirstAsync(hotbar => hotbar.Id == inventoryRemoveItemRequest.PlayerId, cancellationToken: cancellationToken);

        object result;
        if (inventoryRemoveItemRequest.InstanceId is not null)
        {
            if (await InventoryUtils.TakeInstanceItemsAsync(earthDb, ResultsEF.Builder.Null, inventoryRemoveItemRequest.PlayerId, inventoryRemoveItemRequest.ItemId, [inventoryRemoveItemRequest.InstanceId.Value], cancellationToken) is null)
            {
                LogBuildplateInstanceAttemptedToRemoveItemFromPlayerThatIsNotInInventory(instanceId, inventoryRemoveItemRequest.ItemId, inventoryRemoveItemRequest.InstanceId.Value.ToString(), inventoryRemoveItemRequest.PlayerId);
                result = false;
            }
            else
            {
                result = true;
            }
        }
        else
        {
            if (await InventoryUtils.TakeStackableItemsAsync(earthDb, ResultsEF.Builder.Null, inventoryRemoveItemRequest.PlayerId, inventoryRemoveItemRequest.ItemId, inventoryRemoveItemRequest.Count, cancellationToken))
            {
                result = inventoryRemoveItemRequest.Count;
            }
            else
            {
                var count = (await earthDb.StackableItems
                    .AsNoTracking()
                    .FirstOrDefaultAsync(item => item.AccountId == inventoryRemoveItemRequest.PlayerId && item.ItemId == inventoryRemoveItemRequest.ItemId, cancellationToken))?.Count ?? 0;
                if (!await InventoryUtils.TakeStackableItemsAsync(earthDb, ResultsEF.Builder.Null, inventoryRemoveItemRequest.PlayerId, inventoryRemoveItemRequest.ItemId, count, cancellationToken))
                {
                    count = 0;
                }

                LogBuildplateInstanceAttemptedToRemoveItemFromPlayerThatIsNotInInventory(instanceId, inventoryRemoveItemRequest.ItemId, (inventoryRemoveItemRequest.Count - count).ToString(CultureInfo.InvariantCulture), inventoryRemoveItemRequest.PlayerId);
                result = count;
            }
        }

        await HotbarUtils.LimitToInventoryAsync(earthDb, inventoryRemoveItemRequest.PlayerId, hotbar, cancellationToken);

        await earthDb.SaveChangesAsync(cancellationToken);

        return result;
    }

    private async Task<bool> HandleInventoryUpdateWearAsync(Guid instanceId, InventoryUpdateItemWearMessage inventoryUpdateItemWearMessage, CancellationToken cancellationToken = default)
    {
        await using var earthDb = await _earthDbFactory.CreateDbContextAsync(cancellationToken);

        var nonStackableItemInstance = await earthDb.NonStackableItems
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.AccountId == inventoryUpdateItemWearMessage.PlayerId && item.ItemId == inventoryUpdateItemWearMessage.ItemId && item.InstanceId == inventoryUpdateItemWearMessage.InstanceId, cancellationToken);

        if (nonStackableItemInstance is not null)
        {
            // TODO: make NonStackableItemInstance mutable instead of doing this
            if (await InventoryUtils.TakeInstanceItemsAsync(earthDb, ResultsEF.Builder.Null, inventoryUpdateItemWearMessage.PlayerId, inventoryUpdateItemWearMessage.ItemId, [inventoryUpdateItemWearMessage.InstanceId], cancellationToken) is null)
            {
                throw new InvalidOperationException();
            }

            await InventoryUtils.AddInstanceItemsAsync(earthDb, ResultsEF.Builder.Null, [new NonStackableItemInstanceEF(inventoryUpdateItemWearMessage.PlayerId, inventoryUpdateItemWearMessage.ItemId, inventoryUpdateItemWearMessage.InstanceId, inventoryUpdateItemWearMessage.Wear)], cancellationToken);

        }
        else
        {
            LogBuildplateInstanceAttemptedToUpdateItemWearForItemPlayerThatIsNotInInventory(instanceId, inventoryUpdateItemWearMessage.ItemId, inventoryUpdateItemWearMessage.InstanceId, inventoryUpdateItemWearMessage.PlayerId);
        }

        return true;
    }

#pragma warning disable IDE0060 // Remove unused parameter
    private async Task<bool> HandleInventorySetHotbarAsync(Guid instanceId, InventorySetHotbarMessage inventorySetHotbarMessage, CancellationToken cancellationToken = default)
#pragma warning restore IDE0060 // Remove unused parameter
    {
        await using var earthDb = await _earthDbFactory.CreateDbContextAsync(cancellationToken);

        var hotbar = await earthDb.Hotbars
            .AsTracking()
            .FirstAsync(hotbar => hotbar.Id == inventorySetHotbarMessage.PlayerId, cancellationToken: cancellationToken);

        for (var index = 0; index < hotbar.Items.Length; index++)
        {
            InventorySetHotbarMessage.Item item = inventorySetHotbarMessage.Items[index];
            hotbar.Items[index] = item is not null ? new HotbarEF.Item(item.ItemId, item.Count, item.InstanceId) : null;
        }

        await HotbarUtils.LimitToInventoryAsync(earthDb, inventorySetHotbarMessage.PlayerId, hotbar, cancellationToken);

        await earthDb.SaveChangesAsync(cancellationToken);

        return true;
    }

    private static RequestWithInstanceId<T>? ReadRequest<T>(string str, ILogger logger)
    {
        try
        {
            RequestWithInstanceId<T>? request = Json.Deserialize<RequestWithInstanceId<T>>(str);
            return request;
        }
        catch (Exception exception)
        {
            LogBadJsonInBuildplatesEventBusRequest(logger, exception);
            return null;
        }
    }

    private static T? ReadRawRequest<T>(string str, ILogger logger)
    {
        try
        {
            T? request = Json.Deserialize<T>(str);
            return request;
        }
        catch (Exception exception)
        {
            LogBadJsonInBuildplatesEventBusRequest(logger, exception);
            return default;
        }
    }

    private sealed record RequestWithInstanceId<T>(
        Guid InstanceId,
        T Request
    );

    [LoggerMessage(Level = LogLevel.Error, Message = "Database error while handling request")]
    private partial void LogDatabaseErrorWhileHandlingRequest(Exception exception);

    [LoggerMessage(Level = LogLevel.Critical, Message = "Buildplates event bus request handler error")]
    private partial void LogBuildplatesEventBusRequestHandlerError(Exception? exception);

    [LoggerMessage(Level = LogLevel.Error, Message = "World data object {ServerDataObjectId} for buildplate {BuildplateId} could not be loaded from object store")]
    private partial void LogFailedToGetServerData(Guid ServerDataObjectId, Guid BuildplateId);

    [LoggerMessage(Level = LogLevel.Error, Message = "World data object {ServerDataObjectId} for shared buildplate {BuildplateId} could not be loaded from object store")]
    private partial void LogFailedToGetServerDataShared(Guid ServerDataObjectId, Guid BuildplateId);

    [LoggerMessage(Level = LogLevel.Error, Message = "World data object {ServerDataObjectId} for encounter buildplate {BuildplateId} could not be loaded from object store")]
    private partial void LogFailedToGetServerDataEncounter(Guid ServerDataObjectId, Guid BuildplateId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not generate preview for buildplate")]
    private partial void LogCouldNotGeneratePreviewForBuildplate();

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to store new world data object for buildplate {BuildplateId} in object store")]
    private partial void LogFailedToStoreData(Guid BuildplateId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to store new preview object for buildplate {BuildplateId} in object store")]
    private partial void LogFailedToStorePreview(Guid BuildplateId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Stored new snapshot for buildplate {BuildplateId}")]
    private partial void LogStoredNewSnapshotForBuildplate(Guid BuildplateId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Error saving world")]
    private partial void LogErrorSavingWorld(Exception exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Unknown instance type '{Type}' in HandlePlayerConnected")]
    private partial void LogExpectedBackpackContentsInPlayerDisconnectedRequest(BuildplateInstancesManager.InstanceType Type);

    [LoggerMessage(Level = LogLevel.Error, Message = "Expected backpack contents in player disconnected request")]
    private partial void LogExpectedBackpackContentsInPlayerDisconnectedRequest();

    [LoggerMessage(Level = LogLevel.Error, Message = "Backpack contents contained item that is not in item catalog")]
    private partial void LogBackpackContentsContainedItemThatIsNotInItemCatalog();

    [LoggerMessage(Level = LogLevel.Error, Message = "Backpack contents contained non-stackable item without instance ID")]
    private partial void LogBackpackContentsContainedNonStackableItemWithoutInstanceId();

    [LoggerMessage(Level = LogLevel.Warning, Message = "Buildplate instance {InstanceId} attempted to remove item {ItemId} {ItemInstanceOrCount} from player {AccountId} that is not in inventory")]
    private partial void LogBuildplateInstanceAttemptedToRemoveItemFromPlayerThatIsNotInInventory(Guid InstanceId, Guid ItemId, string ItemInstanceOrCount, Guid AccountId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Buildplate instance {InstanceId} attempted to update item wear for item {ItemId} {ItemInstanceId} player {AccountId} that is not in inventory")]
    private partial void LogBuildplateInstanceAttemptedToUpdateItemWearForItemPlayerThatIsNotInInventory(Guid InstanceId, Guid ItemId, Guid ItemInstanceId, Guid AccountId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Bad JSON in buildplates event bus request")]
    private static partial void LogBadJsonInBuildplatesEventBusRequest(ILogger logger, Exception exception);
}
