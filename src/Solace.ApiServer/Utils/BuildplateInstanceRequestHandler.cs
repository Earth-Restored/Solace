using Serilog;
using System.Diagnostics;
using System.Text;
using Solace.Buildplate.Connector.Model;
using Solace.Common;
using Solace.Common.Utils;
using Solace.DB;
using Solace.DB.Models.Common;
using Solace.DB.Models.Player;
using Solace.EventBus.Client;
using Solace.ObjectStore.Client;
using Solace.StaticData;
using CICIBIEType = Solace.StaticData.Catalog.ItemsCatalogR.Item.BoostInfoR.Effect.TypeE;
using Microsoft.EntityFrameworkCore;
using Solace.DB.Utils;

namespace Solace.ApiServer.Utils;

public sealed class BuildplateInstanceRequestHandler
{
    public static void Start(EarthDbContext earthDB, EventBusClient eventBusClient, ObjectStoreClient objectStoreClient, Catalog catalog, BuildplateInstancesManager buildplateInstancesManager)
        => CreateAsync(earthDB, eventBusClient, objectStoreClient, catalog, buildplateInstancesManager).Forget();

    private readonly EarthDbContext _earthDB;
    private readonly ObjectStoreClient _objectStoreClient;
    private readonly Catalog _catalog;
    private readonly BuildplateInstancesManager _buildplateInstancesManager;

    private BuildplateInstanceRequestHandler(EarthDbContext earthDB, ObjectStoreClient objectStoreClient, Catalog catalog, BuildplateInstancesManager buildplateInstancesManager)
    {
        _earthDB = earthDB;
        _objectStoreClient = objectStoreClient;
        _catalog = catalog;
        _buildplateInstancesManager = buildplateInstancesManager;
    }

    public static async Task<BuildplateInstanceRequestHandler> CreateAsync(EarthDbContext earthDB, EventBusClient eventBusClient, ObjectStoreClient objectStoreClient, Catalog catalog, BuildplateInstancesManager buildplateInstancesManager)
    {
        var buildplateInstanceRequestHandler = new BuildplateInstanceRequestHandler(earthDB, objectStoreClient, catalog, buildplateInstancesManager);

        RequestHandler requestHandler = await eventBusClient.AddRequestHandlerAsync("buildplates", new RequestHandlerLister(
            async request =>
            {
                try
                {
                    switch (request.Type)
                    {
                        case "load":
                            {
                                BuildplateLoadRequest? buildplateLoadRequest = ReadRawRequest<BuildplateLoadRequest>(request.Data);
                                if (buildplateLoadRequest is null)
                                {
                                    return null;
                                }

                                BuildplateLoadResponse? buildplateLoadResponse = await buildplateInstanceRequestHandler.HandleLoad(buildplateLoadRequest.PlayerId, buildplateLoadRequest.BuildplateId);
                                return buildplateLoadResponse is not null ? Json.Serialize(buildplateLoadResponse) : null;
                            }
                        case "loadShared":
                            {
                                SharedBuildplateLoadRequest? sharedBuildplateLoadRequest = ReadRawRequest<SharedBuildplateLoadRequest>(request.Data);
                                if (sharedBuildplateLoadRequest is null)
                                {
                                    return null;
                                }

                                BuildplateLoadResponse? buildplateLoadResponse = await buildplateInstanceRequestHandler.HandleLoadShared(sharedBuildplateLoadRequest.SharedBuildplateId);
                                return buildplateLoadResponse is not null ? Json.Serialize(buildplateLoadResponse) : null;
                            }
                        case "loadEncounter":

                            {
                                EncounterBuildplateLoadRequest? encounterBuildplateLoadRequest = ReadRawRequest<EncounterBuildplateLoadRequest>(request.Data);
                                if (encounterBuildplateLoadRequest is null)
                                {
                                    return null;
                                }

                                BuildplateLoadResponse? buildplateLoadResponse = await buildplateInstanceRequestHandler.HandleLoadEncounter(encounterBuildplateLoadRequest.EncounterBuildplateId);
                                return buildplateLoadResponse is not null ? Json.Serialize(buildplateLoadResponse) : null;
                            }
                        case "saved":
                            {
                                RequestWithInstanceId<WorldSavedMessage>? requestWithInstanceId = ReadRequest<WorldSavedMessage>(request.Data);
                                return requestWithInstanceId is null
                                    ? null
                                    : await buildplateInstanceRequestHandler.HandleSaved(requestWithInstanceId.InstanceId, requestWithInstanceId.Request.DataBase64, request.Timestamp) ? "" : null;
                            }
                        case "playerConnected":
                            {
                                Log.Debug("RequestHandler playerConnected");
                                RequestWithInstanceId<PlayerConnectedRequest>? requestWithInstanceId = ReadRequest<PlayerConnectedRequest>(request.Data);
                                if (requestWithInstanceId is null)
                                {
                                    return null;
                                }

                                PlayerConnectedResponse? playerConnectedResponse = await buildplateInstanceRequestHandler.HandlePlayerConnected(requestWithInstanceId.InstanceId, requestWithInstanceId.Request);
                                return playerConnectedResponse is not null ? Json.Serialize(playerConnectedResponse) : null;
                            }
                        case "playerDisconnected":
                            {
                                RequestWithInstanceId<PlayerDisconnectedRequest>? requestWithInstanceId = ReadRequest<PlayerDisconnectedRequest>(request.Data);
                                if (requestWithInstanceId is null)
                                {
                                    return null;
                                }

                                PlayerDisconnectedResponse? playerDisconnectedResponse = await buildplateInstanceRequestHandler.HandlePlayerDisconnected(requestWithInstanceId.InstanceId, requestWithInstanceId.Request, request.Timestamp);
                                return playerDisconnectedResponse is not null ? Json.Serialize(playerDisconnectedResponse) : null;
                            }
                        case "playerDead":
                            {
                                RequestWithInstanceId<Guid>? requestWithInstanceId = ReadRequest<Guid>(request.Data);
                                if (requestWithInstanceId is null)
                                {
                                    return null;
                                }

                                bool? respawn = buildplateInstanceRequestHandler.HandlePlayerDead(requestWithInstanceId.InstanceId, requestWithInstanceId.Request, request.Timestamp);
                                return respawn is not null ? Json.Serialize(respawn.Value) : null;
                            }
                        case "getInitialPlayerState":
                            {
                                RequestWithInstanceId<Guid>? requestWithInstanceId = ReadRequest<Guid>(request.Data);
                                if (requestWithInstanceId is null)
                                {
                                    return null;
                                }

                                InitialPlayerStateResponse? initialPlayerStateResponse = await buildplateInstanceRequestHandler.HandleGetInitialPlayerState(requestWithInstanceId.InstanceId, requestWithInstanceId.Request, request.Timestamp);
                                return initialPlayerStateResponse is not null ? Json.Serialize(initialPlayerStateResponse) : null;
                            }
                        case "getInventory":
                            {
                                RequestWithInstanceId<Guid>? requestWithInstanceId = ReadRequest<Guid>(request.Data);
                                if (requestWithInstanceId is null)
                                {
                                    return null;
                                }

                                InventoryResponse? inventoryResponse = await buildplateInstanceRequestHandler.HandleGetInventory(requestWithInstanceId.InstanceId, requestWithInstanceId.Request);
                                return inventoryResponse is not null ? Json.Serialize(inventoryResponse) : null;
                            }
                        case "inventoryAdd":
                            {
                                RequestWithInstanceId<InventoryAddItemMessage>? requestWithInstanceId = ReadRequest<InventoryAddItemMessage>(request.Data);
                                return requestWithInstanceId is null
                                    ? null
                                    : await buildplateInstanceRequestHandler.HandleInventoryAdd(requestWithInstanceId.InstanceId, requestWithInstanceId.Request, request.Timestamp) ? "" : null;
                            }
                        case "inventoryRemove":
                            {
                                RequestWithInstanceId<InventoryRemoveItemRequest>? requestWithBuildplateId = ReadRequest<InventoryRemoveItemRequest>(request.Data);
                                if (requestWithBuildplateId is null)
                                {
                                    return null;
                                }

                                object response = await buildplateInstanceRequestHandler.HandleInventoryRemove(requestWithBuildplateId.InstanceId, requestWithBuildplateId.Request);
                                return response is not null ? Json.Serialize(response) : null;
                            }
                        case "inventoryUpdateWear":
                            {
                                RequestWithInstanceId<InventoryUpdateItemWearMessage>? requestWithInstanceId = ReadRequest<InventoryUpdateItemWearMessage>(request.Data);

                                return requestWithInstanceId is null
                                    ? null
                                    : await buildplateInstanceRequestHandler.HandleInventoryUpdateWear(requestWithInstanceId.InstanceId, requestWithInstanceId.Request) ? "" : null;
                            }
                        case "inventorySetHotbar":
                            {
                                RequestWithInstanceId<InventorySetHotbarMessage>? requestWithInstanceId = ReadRequest<InventorySetHotbarMessage>(request.Data);

                                return requestWithInstanceId is null
                                    ? null
                                    : await buildplateInstanceRequestHandler.HandleInventorySetHotbar(requestWithInstanceId.InstanceId, requestWithInstanceId.Request) ? "" : null;
                            }
                        default:
                            return null;
                    }
                }
                catch (Exception ex) when (ex is DbUpdateException or DbUpdateConcurrencyException)
                {
                    Log.Error($"Database error while handling request: {ex}");
                    return null;
                }
            },
            async () =>
            {
                Log.Fatal("Buildplates event bus request handler error");
                Log.CloseAndFlush();
                Environment.Exit(1);
            }
        ));

        return buildplateInstanceRequestHandler;
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

    private async Task<BuildplateLoadResponse?> HandleLoad(Guid accountId, Guid buildplateId)
    {
        var buildplate = await _earthDB.PlayerBuildplates
            .AsNoTracking()
            .FirstOrDefaultAsync(buildplate => buildplate.Id == buildplateId && buildplate.AccountId == accountId);

        if (buildplate is null)
        {
            return null;
        }

        byte[]? serverData = await _objectStoreClient.GetAsync(buildplate.ServerDataObjectId);
        if (serverData is null)
        {
            Log.Error($"Data object {buildplate.ServerDataObjectId} for buildplate {buildplateId} could not be loaded from object store");
            return null;
        }

        string serverDataBase64 = Convert.ToBase64String(serverData);

        return new BuildplateLoadResponse(serverDataBase64);
    }

    private async Task<BuildplateLoadResponse?> HandleLoadShared(Guid sharedBuildplateId)
    {
        var sharedBuildplate = await _earthDB.SharedBuildplates
            .AsNoTracking()
            .FirstOrDefaultAsync(sharedBuildplate => sharedBuildplate.Id == sharedBuildplateId);

        if (sharedBuildplate is null)
        {
            return null;
        }

        byte[]? serverData = await _objectStoreClient.GetAsync(sharedBuildplate.ServerDataObjectId);
        if (serverData is null)
        {
            Log.Error($"Data object {sharedBuildplate.ServerDataObjectId} for shared buildplate {sharedBuildplateId} could not be loaded from object store");
            return null;
        }

        string serverDataBase64 = Convert.ToBase64String(serverData);

        return new BuildplateLoadResponse(serverDataBase64);
    }

    private async Task<BuildplateLoadResponse?> HandleLoadEncounter(Guid encounterBuildplateId)
    {
        var encounterBuildplate = await _earthDB.EncounterBuildplates
            .AsNoTracking()
            .FirstOrDefaultAsync(encounterBuildplate => encounterBuildplate.Id == encounterBuildplateId);

        if (encounterBuildplate is null)
        {
            return null;
        }

        byte[]? serverData = await _objectStoreClient.GetAsync(encounterBuildplate.ServerDataObjectId);
        if (serverData is null)
        {
            Log.Error($"Data object {encounterBuildplate.ServerDataObjectId} for encounter buildplate {encounterBuildplateId} could not be loaded from object store");
            return null;
        }

        string serverDataBase64 = Convert.ToBase64String(serverData);

        return new BuildplateLoadResponse(serverDataBase64);
    }

    private async Task<bool> HandleSaved(Guid instanceId, string dataBase64, long timestamp)
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

        var buildplateUnsafeForPreviewGenerator = await _earthDB.PlayerBuildplates
            .AsNoTracking()
            .FirstOrDefaultAsync(buildplate => buildplate.Id == buildplateId);

        if (buildplateUnsafeForPreviewGenerator is null)
        {
            return false;
        }

        string? preview = await _buildplateInstancesManager.GetBuildplatePreviewAsync(serverData, buildplateUnsafeForPreviewGenerator.Night);
        if (preview is null)
        {
            Log.Warning("Could not generate preview for buildplate");
        }

        string? serverDataObjectId = await _objectStoreClient.StoreAsync(serverData);
        if (serverDataObjectId is null)
        {
            Log.Error($"Could not store new data object for buildplate {buildplateId} in object store");
            return false;
        }

        string? previewObjectId;
        if (preview is not null)
        {
            previewObjectId = await _objectStoreClient.StoreAsync(Encoding.ASCII.GetBytes(preview));
            if (previewObjectId is null)
            {
                Log.Warning($"Could not store new preview object for buildplate {buildplateId} in object store");
            }
        }
        else
        {
            previewObjectId = null;
        }

        try
        {
            var buildplate = await _earthDB.PlayerBuildplates
                .AsTracking()
                .FirstOrDefaultAsync(buildplate => buildplate.Id == buildplateId && buildplate.AccountId == accountId);

            if (buildplate is null)
            {
                await _objectStoreClient.DeleteAsync(serverDataObjectId);
                if (previewObjectId is not null)
                {
                    await _objectStoreClient.DeleteAsync(previewObjectId);
                }

                return false;
            }

            string oldServerDataObjectId = buildplate.ServerDataObjectId;

            buildplate.LastModified = timestamp;
            buildplate.ServerDataObjectId = serverDataObjectId;

            string oldPreviewObjectId;
            if (previewObjectId is not null)
            {
                oldPreviewObjectId = buildplate.PreviewObjectId;
                buildplate.PreviewObjectId = previewObjectId;
            }
            else
            {
                oldPreviewObjectId = "";
            }

            await _earthDB.SaveChangesAsync();

            await _objectStoreClient.DeleteAsync(oldServerDataObjectId);

            if (!string.IsNullOrEmpty(oldPreviewObjectId))
            {
                await _objectStoreClient.DeleteAsync(oldPreviewObjectId);
            }

            Log.Information($"Stored new snapshot for buildplate {buildplateId}");

            return true;
        }
        catch (Exception ex) when (ex is DbUpdateException or DbUpdateConcurrencyException)
        {
            Log.Error(ex, $"Error saving world: {ex.Message}");

            await _objectStoreClient.DeleteAsync(serverDataObjectId);
            if (previewObjectId is not null)
            {
                await _objectStoreClient.DeleteAsync(previewObjectId);
            }

            throw;
        }
    }

    private async Task<PlayerConnectedResponse?> HandlePlayerConnected(Guid instanceId, PlayerConnectedRequest playerConnectedRequest)
    {
        // TODO: check join code etc.

        BuildplateInstancesManager.InstanceInfo? instanceInfo = _buildplateInstancesManager.GetInstanceInfo(instanceId);

        if (instanceInfo is null)
        {
            return null;
        }

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
                    var inventory = await _earthDB.Inventories
                        .AsNoTracking()
                        .FirstOrNewAsync(invenotry => invenotry.Id == playerConnectedRequest.Uuid, trackNew: false);

                    var hotbar = await _earthDB.Hotbars
                        .AsNoTracking()
                        .FirstOrNewAsync(hotbar => hotbar.Id == playerConnectedRequest.Uuid, trackNew: false);

                    initialInventoryContents = new InventoryResponse(
                        [.. Enumerable.Concat(
                            inventory.StackableItems
                                .Select(item => new InventoryResponse.Item(item.Id, item.Count, null, 0)),
                            inventory.NonStackableItems
                                .SelectMany(item => item.Instances
                                    .Select(instance => new InventoryResponse.Item(item.Id, 1, instance.InstanceId, instance.Wear)))
                        ).Where(item => item.Count > 0)],
                        [.. hotbar.Items.Select(item => item is { Count: > 0 } ? new InventoryResponse.HotbarItem(item.Uuid, item.Count, item.InstanceId) : null)]
                    );
                }

                break;
            case BuildplateInstancesManager.InstanceType.SHARED_BUILD or BuildplateInstancesManager.InstanceType.SHARED_PLAY:
                {
                    var sharedBuildplate = await _earthDB.SharedBuildplates
                        .AsNoTracking()
                        .FirstOrDefaultAsync(sharedBuildplate => sharedBuildplate.Id == instanceInfo.BuildplateId);

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
                    var inventory = await _earthDB.Inventories
                        .AsTracking()
                        .FirstOrNewAsync(invenotry => invenotry.Id == playerConnectedRequest.Uuid);

                    var hotbar = await _earthDB.Hotbars
                        .AsTracking()
                        .FirstOrNewAsync(hotbar => hotbar.Id == playerConnectedRequest.Uuid);

                    var inventoryResponseHotbar = new InventoryResponse.HotbarItem[7];
                    Dictionary<string, int> inventoryResponseStackableItems = [];
                    LinkedList<InventoryResponse.Item> inventoryResponseNonStackableItems = [];
                    for (int index = 0; index < 7; index++)
                    {
                        var item = hotbar.Items[index];
                        if (item is not null)
                        {
                            if (item.InstanceId is null)
                            {
                                inventory.TakeItems(item.Uuid, item.Count);
                                inventoryResponseStackableItems[item.Uuid] = inventoryResponseStackableItems.GetValueOrDefault(item.Uuid, 0) + item.Count;
                                inventoryResponseHotbar[index] = new InventoryResponse.HotbarItem(item.Uuid, item.Count, null);
                            }
                            else
                            {
                                int wear = inventory.TakeItems(item.Uuid, [item.InstanceId])!.First().Wear;
                                inventoryResponseNonStackableItems.AddLast(new InventoryResponse.Item(item.Uuid, 1, item.InstanceId, wear));
                                inventoryResponseHotbar[index] = new InventoryResponse.HotbarItem(item.Uuid, 1, item.InstanceId);
                            }
                        }
                    }

                    hotbar.LimitToInventory(inventory);

                    initialInventoryContents = new InventoryResponse(
                        [
                            .. inventoryResponseStackableItems.Select(entry => new InventoryResponse.Item(entry.Key, entry.Value, null, 0)),
                            .. inventoryResponseNonStackableItems
                        ],
                        inventoryResponseHotbar
                    );

                    await _earthDB.SaveChangesAsync();
                }

                break;
            default:
                {
                    // shouldn't happen, safe default
                    Log.Warning($"Unknown instance type '{instanceInfo.Type}' in HandlePlayerConnected.");
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

    private async Task<PlayerDisconnectedResponse?> HandlePlayerDisconnected(Guid instanceId, PlayerDisconnectedRequest playerDisconnectedRequest, long timestamp)
    {
        BuildplateInstancesManager.InstanceInfo? instanceInfo = _buildplateInstancesManager.GetInstanceInfo(instanceId);
        if (instanceInfo is null)
        {
            return null;
        }

        bool usesBackpack = instanceInfo.Type == BuildplateInstancesManager.InstanceType.ENCOUNTER;
        if (usesBackpack)
        {
            InventoryResponse? backpackContents = playerDisconnectedRequest.BackpackContents;
            if (backpackContents is null)
            {
                Log.Error("Expected backpack contents in player disconnected request");
                return null;
            }

            var inventory = await _earthDB.Inventories
                .AsTracking()
                .FirstOrNewAsync(invenotry => invenotry.Id == playerDisconnectedRequest.PlayerId);

            var hotbar = await _earthDB.Hotbars
                .AsTracking()
                .FirstOrNewAsync(hotbar => hotbar.Id == playerDisconnectedRequest.PlayerId);

            var journal = await _earthDB.Journals
                .AsTracking()
                .FirstOrNewAsync(journal => journal.Id == playerDisconnectedRequest.PlayerId);

            LinkedList<string> unlockedJournalItems = [];
            foreach (InventoryResponse.Item item in backpackContents.Items)
            {
                Catalog.ItemsCatalogR.Item? catalogItem = _catalog.ItemsCatalog.GetItem(item.Id);
                if (catalogItem is null)
                {
                    Log.Error("Backpack contents contained item that is not in item catalog");
                    continue;
                }

                if (!catalogItem.Stackable && item.InstanceId is null)
                {
                    Log.Error("Backpack contents contained non-stackable item without instance ID");
                    continue;
                }

                if (catalogItem.Stackable)
                {
                    inventory.AddItems(item.Id, item.Count);
                }
                else
                {
                    Debug.Assert(item.InstanceId is not null);

                    inventory.AddItems(item.Id, [new NonStackableItemInstance(item.InstanceId, item.Wear)]);
                }

                if (journal.AddCollectedItem(item.Id, timestamp, item.Count) == 0)
                {
                    if (catalogItem.JournalEntry is not null)
                    {
                        unlockedJournalItems.AddLast(item.Id);
                    }
                }
            }

            for (int index = 0; index < 7; index++)
            {
                InventoryResponse.HotbarItem? hotbarItem = backpackContents.Hotbar[index];
                if (hotbarItem is not null)
                {
                    hotbar.Items[index] = new HotbarEF.Item(hotbarItem.Id, hotbarItem.Count, hotbarItem.InstanceId);
                }
            }

            hotbar.LimitToInventory(inventory);

            await _earthDB.SaveChangesAsync();

            foreach (string itemId in unlockedJournalItems)
            {
                await TokenUtils.AddTokenAsync(new EarthDbContext.Results(_earthDB), playerDisconnectedRequest.PlayerId, new TokensEF.JournalItemUnlockedToken(itemId));
            }
        }

        return new PlayerDisconnectedResponse();
    }

#pragma warning disable IDE0060 // Remove unused parameter
    private bool? HandlePlayerDead(Guid instanceId, Guid playerId, long currentTime)
#pragma warning restore IDE0060 // Remove unused parameter
    {
        BuildplateInstancesManager.InstanceInfo? instanceInfo = _buildplateInstancesManager.GetInstanceInfo(instanceId);
        return instanceInfo is null
            ? null
            : instanceInfo.Type is BuildplateInstancesManager.InstanceType.BUILD or BuildplateInstancesManager.InstanceType.SHARED_BUILD;
    }

    private sealed record EffectInfo(
        long EndTime,
        Catalog.ItemsCatalogR.Item.BoostInfoR.Effect Effect
    );
    private async Task<InitialPlayerStateResponse?> HandleGetInitialPlayerState(Guid instanceId, Guid accountId, long currentTime)
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

            var profile = await _earthDB.Profiles
                .AsNoTracking()
                .FirstOrNewAsync(profile => profile.Id == accountId, trackNew: false);

            var boosts = await _earthDB.Boosts
                .AsNoTracking()
                .FirstOrNewAsync(boosts => boosts.Id == accountId, trackNew: false);

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
    private async Task<InventoryResponse?> HandleGetInventory(Guid instanceId, Guid requestedInventoryAccountId)
#pragma warning restore IDE0060 // Remove unused parameter
    {
        var inventory = await _earthDB.Inventories
            .AsNoTracking()
            .FirstOrNewAsync(inventory => inventory.Id == requestedInventoryAccountId, trackNew: false);

        var hotbar = await _earthDB.Hotbars
            .AsNoTracking()
            .FirstOrNewAsync(hotbar => hotbar.Id == requestedInventoryAccountId, trackNew: false);

        return new InventoryResponse(
            [.. Enumerable.Concat(
                inventory.StackableItems
                    .Select(item => new InventoryResponse.Item(item.Id, item.Count, null, 0)),
                inventory.NonStackableItems
                    .SelectMany(item => item.Instances
                    .Select(instance => new InventoryResponse.Item(item.Id, 1, instance.InstanceId, instance.Wear)))
            ).Where(item => item.Count > 0)],
            [.. hotbar.Items.Select(item => item is not null && item.Count > 0 ? new InventoryResponse.HotbarItem(item.Uuid, item.Count, item.InstanceId) : null)]
        );
    }

#pragma warning disable IDE0060 // Remove unused parameter
    private async Task<bool> HandleInventoryAdd(Guid instanceId, InventoryAddItemMessage inventoryAddItemMessage, long timestamp)
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

        var inventory = await _earthDB.Inventories
            .AsTracking()
            .FirstOrNewAsync(inventory => inventory.Id == inventoryAddItemMessage.PlayerId);

        var journal = await _earthDB.Journals
            .AsTracking()
            .FirstOrNewAsync(journal => journal.Id == inventoryAddItemMessage.PlayerId);

        if (catalogItem.Stackable)
        {
            inventory.AddItems(inventoryAddItemMessage.ItemId, inventoryAddItemMessage.Count);
        }
        else
        {
            inventory.AddItems(inventoryAddItemMessage.ItemId, [new NonStackableItemInstance(inventoryAddItemMessage.InstanceId!, inventoryAddItemMessage.Wear)]);
        }

        bool journalItemUnlocked = false;
        if (journal.AddCollectedItem(inventoryAddItemMessage.ItemId, timestamp, inventoryAddItemMessage.Count) == 0)
        {
            if (catalogItem.JournalEntry is not null)
            {
                journalItemUnlocked = true;
            }
        }

        await _earthDB.SaveChangesAsync();

        if (journalItemUnlocked)
        {
            await TokenUtils.AddTokenAsync(new EarthDbContext.Results(_earthDB), inventoryAddItemMessage.PlayerId, new TokensEF.JournalItemUnlockedToken(inventoryAddItemMessage.ItemId));
        }

        return true;
    }

    private async Task<object> HandleInventoryRemove(Guid instanceId, InventoryRemoveItemRequest inventoryRemoveItemRequest)
    {
        var inventory = await _earthDB.Inventories
            .AsTracking()
            .FirstOrNewAsync(inventory => inventory.Id == inventoryRemoveItemRequest.PlayerId);

        var hotbar = await _earthDB.Hotbars
            .AsTracking()
            .FirstOrNewAsync(hotbar => hotbar.Id == inventoryRemoveItemRequest.PlayerId);

        object result;
        if (inventoryRemoveItemRequest.InstanceId is not null)
        {
            if (inventory.TakeItems(inventoryRemoveItemRequest.ItemId, [inventoryRemoveItemRequest.InstanceId]) is null)
            {
                Log.Warning($"Buildplate instance {instanceId} attempted to remove item {inventoryRemoveItemRequest.ItemId} {inventoryRemoveItemRequest.InstanceId} from player {inventoryRemoveItemRequest.PlayerId} that is not in inventory");
                result = false;
            }
            else
            {
                result = true;
            }
        }
        else
        {
            if (inventory.TakeItems(inventoryRemoveItemRequest.ItemId, inventoryRemoveItemRequest.Count))
            {
                result = inventoryRemoveItemRequest.Count;
            }
            else
            {
                int count = inventory.GetItemCount(inventoryRemoveItemRequest.ItemId);
                if (!inventory.TakeItems(inventoryRemoveItemRequest.ItemId, count))
                {
                    count = 0;
                }

                Log.Warning($"Buildplate instance {instanceId} attempted to remove item {inventoryRemoveItemRequest.ItemId} {inventoryRemoveItemRequest.Count - count} from player {inventoryRemoveItemRequest.PlayerId} that is not in inventory");
                result = count;
            }
        }

        hotbar.LimitToInventory(inventory);

        await _earthDB.SaveChangesAsync();

        return result;
    }

    private async Task<bool> HandleInventoryUpdateWear(Guid instanceId, InventoryUpdateItemWearMessage inventoryUpdateItemWearMessage)
    {
        var inventory = await _earthDB.Inventories
            .AsTracking()
            .FirstOrNewAsync(inventory => inventory.Id == inventoryUpdateItemWearMessage.PlayerId);

        NonStackableItemInstance? nonStackableItemInstance = inventory.GetItemInstance(inventoryUpdateItemWearMessage.ItemId, inventoryUpdateItemWearMessage.InstanceId);
        if (nonStackableItemInstance is not null)
        {
            // TODO: make NonStackableItemInstance mutable instead of doing this
            if (inventory.TakeItems(inventoryUpdateItemWearMessage.ItemId, [inventoryUpdateItemWearMessage.InstanceId]) is null)
            {
                throw new InvalidOperationException();
            }

            inventory.AddItems(inventoryUpdateItemWearMessage.ItemId, [new NonStackableItemInstance(inventoryUpdateItemWearMessage.InstanceId, inventoryUpdateItemWearMessage.Wear)]);
        }
        else
        {
            Log.Warning($"Buildplate instance {instanceId} attempted to update item wear for item {inventoryUpdateItemWearMessage.ItemId} {inventoryUpdateItemWearMessage.InstanceId} player {inventoryUpdateItemWearMessage.PlayerId} that is not in inventory");
        }

        await _earthDB.SaveChangesAsync();

        return true;
    }

#pragma warning disable IDE0060 // Remove unused parameter
    private async Task<bool> HandleInventorySetHotbar(Guid instanceId, InventorySetHotbarMessage inventorySetHotbarMessage)
#pragma warning restore IDE0060 // Remove unused parameter
    {
        var inventory = await _earthDB.Inventories
            .AsNoTracking()
            .FirstOrNewAsync(inventory => inventory.Id == inventorySetHotbarMessage.PlayerId, trackNew: false);

        var hotbar = await _earthDB.Hotbars
            .AsTracking()
            .FirstOrNewAsync(hotbar => hotbar.Id == inventorySetHotbarMessage.PlayerId);

        for (int index = 0; index < hotbar.Items.Length; index++)
        {
            InventorySetHotbarMessage.Item item = inventorySetHotbarMessage.Items[index];
            hotbar.Items[index] = item is not null ? new HotbarEF.Item(item.ItemId, item.Count, item.InstanceId) : null;
        }

        hotbar.LimitToInventory(inventory);

        await _earthDB.SaveChangesAsync();

        return true;
    }

    private static RequestWithInstanceId<T>? ReadRequest<T>(string str)
    {
        try
        {
            RequestWithInstanceId<T>? request = Json.Deserialize<RequestWithInstanceId<T>>(str);
            return request;
        }
        catch (Exception ex)
        {
            Log.Error($"Bad JSON in buildplates event bus request: {ex}");
            return null;
        }
    }

    private static T? ReadRawRequest<T>(string str)
    {
        try
        {
            T? request = Json.Deserialize<T>(str);
            return request;
        }
        catch (Exception ex)
        {
            Log.Error($"Bad JSON in buildplates event bus request: {ex}");
            return default;
        }
    }

    private sealed record RequestWithInstanceId<T>(
        Guid InstanceId,
        T Request
    );
}
