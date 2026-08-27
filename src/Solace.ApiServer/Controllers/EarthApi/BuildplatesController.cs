using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Serilog;
using System.Diagnostics;
using System.Security.Claims;
using System.Text;
using System.Text.Json.Serialization;
using Solace.ApiServer.Exceptions;
using Solace.ApiServer.Types.Buildplates;
using Solace.ApiServer.Types.Common;
using Solace.ApiServer.Types.Inventory;
using Solace.ApiServer.Utils;
using Solace.Common.Utils;
using Solace.DB;
using Solace.DB.Models.Global;
using Solace.DB.Models.Player;
using Solace.ObjectStore.Client;
using Solace.StaticData;
using LegacyBuildplates = Solace.DB.Models.Player.LegacyBuildplates;
using Microsoft.EntityFrameworkCore;
using Solace.DB.Utils;

namespace Solace.ApiServer.Controllers.EarthApi;

[Authorize]
[ApiVersion("1.1")]
[Route("1/api/v{version:apiVersion}")]
internal sealed class BuildplatesController : SolaceControllerBase
{
    private readonly EarthDbContext _earthDB;
    private readonly BuildplateInstancesManager _buildplateInstancesManager;
    private readonly Catalog _catalog;
    private readonly TappablesManager _tappablesManager;
    private readonly ObjectStoreClient _objectStore;

    public BuildplatesController(EarthDbContext earthDB, BuildplateInstancesManager buildplateInstancesManager, StaticData.StaticData staticData, TappablesManager tappablesManager, ObjectStoreClient objectStore)
    {
        _earthDB = earthDB;
        _buildplateInstancesManager = buildplateInstancesManager;
        _catalog = staticData.Catalog;
        _tappablesManager = tappablesManager;
        _objectStore = objectStore;
    }

    [HttpGet("buildplates")]
    public async Task<Results<ContentHttpResult, BadRequest>> GetBuildplates()
    {
        if (!TryGetAccountId(out var accountId))
        {
            return TypedResults.BadRequest();
        }

        var buildplates = _earthDB.PlayerBuildplates
            .AsNoTracking()
            .Where(buildplate => buildplate.AccountId == accountId);

        OwnedBuildplate[] ownedBuildplates = await Task.WhenAll(buildplates.AsEnumerable().Select(async buildplate =>
        {
            byte[]? previewData = await _objectStore.GetAsync(buildplate.PreviewObjectId);
            if (previewData is null)
            {
                Log.Error($"Preview object {buildplate.PreviewObjectId} for buildplate {buildplate.Id} could not be loaded from object store");
                return null!;
            }

            string model = Encoding.ASCII.GetString(previewData);
            return new OwnedBuildplate(
                buildplate.Id.ToString(),
                "00000000-0000-0000-0000-000000000000",
                new Dimension(buildplate.Size, buildplate.Size),
                new Offset(0, buildplate.Offset, 0),
                buildplate.Scale,
                OwnedBuildplate.TypeE.SURVIVAL,
                SurfaceOrientation.HORIZONTAL,
                model,
                0,    // TODO
                false,    // TODO
                0,    // TODO
                false,    // TODO
                TimeFormatter.FormatTime(buildplate.LastModified),
                0,    // TODO
                ""
            );
        }).Where(ownedBuildplate => ownedBuildplate is not null));

        return EarthJson(ownedBuildplates);
    }

    [HttpPost("multiplayer/buildplate/{buildplateId}/instances")]
    public async Task<Results<ContentHttpResult, InternalServerError, NotFound, BadRequest>> CreateBuildInstance(Guid buildplateId, CancellationToken cancellationToken)
    {
        if (!TryGetAccountId(out var accountId))
        {
            return TypedResults.BadRequest();
        }

        // TODO: coordinates etc.

        return await GetNewBuildplateInstanceResponse(accountId, buildplateId, BuildplateInstancesManager.InstanceType.BUILD, cancellationToken);
    }

    [HttpPost("multiplayer/buildplate/{buildplateId}/play/instances")]
    public async Task<Results<ContentHttpResult, InternalServerError, NotFound, BadRequest>> CreatePlayInstance(Guid buildplateId, CancellationToken cancellationToken)
    {
        if (!TryGetAccountId(out var accountId))
        {
            return TypedResults.BadRequest();
        }

        // TODO: coordinates etc.

        return await GetNewBuildplateInstanceResponse(accountId, buildplateId, BuildplateInstancesManager.InstanceType.PLAY, cancellationToken);
    }

    [HttpPost("buildplates/{buildplateId}/share")]
    public async Task<Results<ContentHttpResult, BadRequest, NotFound, InternalServerError>> ShareBuildplate(Guid buildplateId, CancellationToken cancellationToken)
    {
        if (!TryGetAccountId(out var accountId))
        {
            return TypedResults.BadRequest();
        }

        long requestStartedOn = HttpContext.GetTimestamp();

        var buildplate = await _earthDB.PlayerBuildplates
            .AsNoTracking()
            .FirstOrDefaultAsync(buildplate => buildplate.Id == buildplateId && buildplate.AccountId == accountId, cancellationToken: cancellationToken);

        if (buildplate is null)
        {
            return TypedResults.NotFound();
        }

        var inventory = await _earthDB.Inventories
            .AsNoTracking()
            .FirstOrNewAsync(inventory => inventory.Id == accountId, trackNew: false, cancellationToken: cancellationToken);

        var hotbar = await _earthDB.Hotbars
            .AsNoTracking()
            .FirstOrNewAsync(hotbar => hotbar.Id == accountId, trackNew: false, cancellationToken: cancellationToken);

        byte[]? serverData = await _objectStore.GetAsync(buildplate.ServerDataObjectId);
        if (serverData is null)
        {
            Log.Error($"Data object {buildplate.ServerDataObjectId} for buildplate {buildplateId} could not be loaded from object store");
            return TypedResults.InternalServerError();
        }

        string? sharedBuildplateServerDataObjectId = await _objectStore.StoreAsync(serverData);
        if (sharedBuildplateServerDataObjectId is null)
        {
            Log.Error("Could not store data object for shared buildplate in object store");
            return TypedResults.InternalServerError();
        }

        var sharedBuildplate = new SharedBuildplateEF()
        {
            AccountId = accountId,
            Size = buildplate.Size,
            Offset = buildplate.Offset,
            Scale = buildplate.Scale,
            Night = buildplate.Night,
            Created = requestStartedOn,
            BuildplateLastModifed = buildplate.LastModified,
            ServerDataObjectId = sharedBuildplateServerDataObjectId,
            LastViewed = requestStartedOn,
            NumberOfTimesViewed = 0,
        };

        for (int index = 0; index < 7; index++)
        {
            var item = hotbar.Items[index];
            SharedBuildplateEF.HotbarItem? sharedBuildplateHotbarItem;
            if (item is null)
            {
                sharedBuildplateHotbarItem = null;
            }
            else if (item.InstanceId is null)
            {
                sharedBuildplateHotbarItem = new SharedBuildplateEF.HotbarItem(item.Uuid, item.Count, null, 0);
            }
            else
            {
                sharedBuildplateHotbarItem = new SharedBuildplateEF.HotbarItem(item.Uuid, 1, item.InstanceId, inventory.GetItemInstance(item.Uuid, item.InstanceId)?.Wear ?? 0);
            }

            sharedBuildplate.Hotbar[index] = sharedBuildplateHotbarItem;
        }

        try
        {
            _earthDB.SharedBuildplates.Add(sharedBuildplate);
            await _earthDB.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            Log.Error(ex, $"Failed to store shared buildplate: {ex.Message}");
            await _objectStore.DeleteAsync(sharedBuildplateServerDataObjectId);
            return TypedResults.InternalServerError();
        }

        return EarthJson($"minecraftearth://sharedbuildplate?id={sharedBuildplate.Id}");
    }

    [HttpGet("buildplates/shared/{sharedBuildplateId}")]
    public async Task<Results<ContentHttpResult, BadRequest, NotFound, InternalServerError>> GetSharedBuildplate(Guid sharedBuildplateId, CancellationToken cancellationToken)
    {
        if (!TryGetAccountId(out var accountId))
        {
            return TypedResults.BadRequest();
        }

        var sharedBuildplate = await _earthDB.SharedBuildplates
            .AsNoTracking()
            .FirstOrDefaultAsync(sharedBuildplate => sharedBuildplate.Id == sharedBuildplateId, cancellationToken: cancellationToken);

        if (sharedBuildplate is null)
        {
            return TypedResults.NotFound();
        }

        byte[]? serverData = await _objectStore.GetAsync(sharedBuildplate.ServerDataObjectId);
        if (serverData is null)
        {
            Log.Error($"Data object {sharedBuildplate.ServerDataObjectId} for shared buildplate {sharedBuildplateId} could not be loaded from object store");
            return TypedResults.InternalServerError();
        }

        string? preview = await _buildplateInstancesManager.GetBuildplatePreviewAsync(serverData, sharedBuildplate.Night);
        if (preview is null)
        {
            Log.Error("Could not get preview for buildplate");
            return TypedResults.InternalServerError();
        }

        return EarthJson(new SharedBuildplate(
            sharedBuildplate.AccountId.ToString(),    // TODO: supposed to return username here, not player ID
            TimeFormatter.FormatTime(sharedBuildplate.Created),
            new SharedBuildplate.BuildplateDataR(
                new Dimension(sharedBuildplate.Size, sharedBuildplate.Size),
                new Offset(0, sharedBuildplate.Offset, 0),
                sharedBuildplate.Scale,
                SharedBuildplate.BuildplateDataR.TypeE.SURVIVAL,
                SurfaceOrientation.HORIZONTAL,
                preview,
                0
            ),
            new Types.Inventory.Inventory(
                [.. sharedBuildplate.Hotbar.Select(item => item is not null ? new HotbarItem(
                    item.Uuid,
                    item.Count,
                    item.InstanceId,
                    item.InstanceId is not null ? ItemWear.WearToHealth(item.Uuid, item.Wear, _catalog.ItemsCatalog) : 0.0f
                ) : null)],
                [.. sharedBuildplate.Hotbar
                    .Where(item => item is not null && item.InstanceId is null)
                    .Select(item => item!.Uuid)
                    .Distinct()
                    .Select(uuid => new StackableInventoryItem(
                        uuid,
                        0,
                        1,
                        // TODO: what unlocked/last seen timestamp are we supposed to use here - the player who shared the buildplate or the player who is viewing the buildplate?
                        new StackableInventoryItem.OnR(TimeFormatter.FormatTime(0)),
                        new StackableInventoryItem.OnR(TimeFormatter.FormatTime(0))
                    ))],
                [.. sharedBuildplate.Hotbar
                    .Where(item => item is not null && item.InstanceId is not null)
                    .Select(item => item!.Uuid)
                    .Distinct()
                    .Select(uuid => new NonStackableInventoryItem(
                        uuid,
                        [],
                        1,
                        // TODO: what unlocked/last seen timestamp are we supposed to use here - the player who shared the buildplate or the player who is viewing the buildplate?
                        new NonStackableInventoryItem.OnR(TimeFormatter.FormatTime(0)),
                        new NonStackableInventoryItem.OnR(TimeFormatter.FormatTime(0))
                    ))]
            )
        ));
    }

    [HttpPost("multiplayer/buildplate/shared/{sharedBuildplateId}/play/instances")]
    public async Task<Results<ContentHttpResult, NotFound, BadRequest, InternalServerError>> GetSharedBuildplateInstance(Guid sharedBuildplateId, CancellationToken cancellationToken)
    {
        if (!TryGetAccountId(out var accountId))
        {
            return TypedResults.BadRequest();
        }

        // TODO: coordinates etc.

        SharedBuildplateInstanceRequest sharedBuildplateInstanceRequest = (await Request.Body.AsJsonAsync<SharedBuildplateInstanceRequest>(cancellationToken))!;

        return await GetNewSharedBuildplateInstanceResponse(accountId, sharedBuildplateId, sharedBuildplateInstanceRequest.FullSize ? BuildplateInstancesManager.InstanceType.SHARED_PLAY : BuildplateInstancesManager.InstanceType.SHARED_BUILD, cancellationToken);
    }

    private sealed record EncounterInstanceRequest(
        string TileId
    );

    [HttpPost("multiplayer/encounters/{encounterId}/instances")]
    public async Task<Results<ContentHttpResult, NotFound, BadRequest, InternalServerError>> CreateEncounterInstance(Guid encounterId, CancellationToken cancellationToken)
    {
        if (!TryGetAccountId(out _))
        {
            return TypedResults.BadRequest();
        }

        var encounterInstanceRequest = await Request.Body.AsJsonAsync<EncounterInstanceRequest>(cancellationToken);

        return encounterInstanceRequest is null
            ? TypedResults.BadRequest()
            : await GetNewEncounterBuildplateInstanceResponse(encounterId, encounterInstanceRequest.TileId, _tappablesManager, cancellationToken);
    }

    // TODO: should we restrict this to matching player ID?
    [HttpGet("multiplayer/partitions/{partitionId}/instances/{instanceId}")]
#pragma warning disable IDE0060 // Remove unused parameter
    public async Task<Results<ContentHttpResult, BadRequest, NotFound>> GetInstanceStatus(Guid partitionId, Guid instanceId, CancellationToken cancellationToken)
#pragma warning restore IDE0060 // Remove unused parameter
    {
        if (!TryGetAccountId(out var accountId))
        {
            return TypedResults.BadRequest();
        }

        BuildplateInstancesManager.InstanceInfo? instanceInfo = _buildplateInstancesManager.GetInstanceInfo(instanceId);
        if (instanceInfo is null || instanceInfo.ShuttingDown)
        {
            return TypedResults.NotFound();
        }

        var buildplate = await _earthDB.PlayerBuildplates
            .AsNoTracking()
            .FirstOrDefaultAsync(buildplate => buildplate.Id == instanceInfo.BuildplateId && buildplate.AccountId == accountId, cancellationToken: cancellationToken);

        if (buildplate is null)
        {
            return TypedResults.NotFound();
        }

        // TODO: the client is supposed to poll until the buildplate server is ready, but instead it just crashes if we tell it that the buildplate server is not ready yet
        // TODO: so instead we just stall the request until it's ready, this is really ugly and eventually we need to figure out why it's crashing and implement this properly
        // TODO: this also relies on the buildplate server starting in less than ~20 seconds as the client will eventually time out the HTTP request and crash anyway
        //BuildplateInstance buildplateInstance = this.instanceInfoToApiResponse(instanceInfo);
        BuildplateInstancesManager.InstanceInfo? instanceInfo1;
        int waitCount = 0;
        do
        {
            instanceInfo1 = _buildplateInstancesManager.GetInstanceInfo(instanceId);
            if (instanceInfo1 is null || instanceInfo1.ShuttingDown)
            {
                return TypedResults.NotFound();
            }

            if (!instanceInfo1.Ready)
            {
                await Task.Delay(1000, cancellationToken);

                waitCount++;
            }
        }
        while (!instanceInfo1.Ready && waitCount < 35);
        BuildplateInstance? buildplateInstance = await InstanceInfoToApiResponse(instanceInfo1, cancellationToken);

        if (buildplateInstance is null)
        {
            return TypedResults.NotFound();
        }

        return EarthJson(buildplateInstance);
    }

    private async Task<Results<ContentHttpResult, InternalServerError, NotFound, BadRequest>> GetNewBuildplateInstanceResponse(Guid accountId, Guid buildplateId, BuildplateInstancesManager.InstanceType type, CancellationToken cancellationToken)
    {
        var buildplate = await _earthDB.PlayerBuildplates
            .AsNoTracking()
            .FirstOrDefaultAsync(buildplate => buildplate.Id == buildplateId, cancellationToken);

        if (buildplate is null)
        {
            return TypedResults.NotFound();
        }

        var instanceId = await _buildplateInstancesManager.RequestBuildplateInstance(accountId, null, buildplateId, type, 0, buildplate.Night);
        if (instanceId is null)
        {
            return TypedResults.InternalServerError();
        }

        var instanceInfo = _buildplateInstancesManager.GetInstanceInfo(instanceId.Value);
        if (instanceInfo is null)
        {
            return TypedResults.InternalServerError();
        }

        var buildplateInstance = await InstanceInfoToApiResponse(instanceInfo, cancellationToken);

        if (buildplateInstance is null)
        {
            return TypedResults.NotFound();
        }

        return EarthJson(buildplateInstance);
    }

    private async Task<Results<ContentHttpResult, NotFound, BadRequest, InternalServerError>> GetNewSharedBuildplateInstanceResponse(Guid accountId, Guid sharedBuildplateId, BuildplateInstancesManager.InstanceType type, CancellationToken cancellationToken)
    {
        var sharedBuildplate = await _earthDB.SharedBuildplates
            .AsNoTracking()
            .FirstOrDefaultAsync(sharedBuildplate => sharedBuildplate.Id == sharedBuildplateId, cancellationToken);

        if (sharedBuildplate is null)
        {
            return TypedResults.NotFound();
        }

        var instanceId = await _buildplateInstancesManager.RequestBuildplateInstance(accountId, null, sharedBuildplateId, type, 0, sharedBuildplate.Night);
        if (instanceId is null)
        {
            return TypedResults.InternalServerError();
        }

        var instanceInfo = _buildplateInstancesManager.GetInstanceInfo(instanceId.Value);
        if (instanceInfo is null)
        {
            return TypedResults.InternalServerError();
        }

        var buildplateInstance = await InstanceInfoToApiResponse(instanceInfo, cancellationToken);
        if (buildplateInstance is null)
        {
            return TypedResults.InternalServerError();
        }

        return EarthJson(buildplateInstance);
    }

    private async Task<Results<ContentHttpResult, NotFound, BadRequest, InternalServerError>> GetNewEncounterBuildplateInstanceResponse(Guid encounterId, string tileId, TappablesManager tappablesManager, CancellationToken cancellationToken)
    {
        var encounter = tappablesManager.GetEncounterWithId(encounterId, tileId);
        if (encounter is null)
        {
            return TypedResults.NotFound();
        }

        var instanceId = await _buildplateInstancesManager.RequestBuildplateInstance(null, encounterId, encounter.EncounterBuildplateId, BuildplateInstancesManager.InstanceType.ENCOUNTER, encounter.SpawnTime + encounter.ValidFor, false);

        if (instanceId is null)
        {
            return TypedResults.InternalServerError();
        }

        var instanceInfo = _buildplateInstancesManager.GetInstanceInfo(instanceId.Value);
        if (instanceInfo is null)
        {
            return TypedResults.InternalServerError();
        }

        var buildplateInstance = await InstanceInfoToApiResponse(instanceInfo, cancellationToken);
        if (buildplateInstance is null)
        {
            return TypedResults.InternalServerError();
        }

        return EarthJson(buildplateInstance);
    }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    private enum Source
    {
        PLAYER,
        SHARED,
        ENCOUNTER
    }

    private async Task<BuildplateInstance?> InstanceInfoToApiResponse(BuildplateInstancesManager.InstanceInfo instanceInfo, CancellationToken cancellationToken)
    {
        var (fullsize, gameplayMode, source) = instanceInfo.Type switch
        {
            BuildplateInstancesManager.InstanceType.BUILD => (false, BuildplateInstance.GameplayMetadataR.GameplayModeE.BUILDPLATE, Source.PLAYER),
            BuildplateInstancesManager.InstanceType.PLAY => (true, BuildplateInstance.GameplayMetadataR.GameplayModeE.BUILDPLATE_PLAY, Source.PLAYER),
            BuildplateInstancesManager.InstanceType.SHARED_BUILD => (true, BuildplateInstance.GameplayMetadataR.GameplayModeE.SHARED_BUILDPLATE_PLAY, Source.SHARED),
            BuildplateInstancesManager.InstanceType.SHARED_PLAY => (true, BuildplateInstance.GameplayMetadataR.GameplayModeE.SHARED_BUILDPLATE_PLAY, Source.SHARED),
            BuildplateInstancesManager.InstanceType.ENCOUNTER => (true, BuildplateInstance.GameplayMetadataR.GameplayModeE.ENCOUNTER, Source.ENCOUNTER),
            _ => throw new UnreachableException(),
        };

        int size;
        int offset;
        int scale;
        switch (source)
        {
            case Source.PLAYER:
                {
                    Debug.Assert(instanceInfo.PlayerId is not null);

                    var buildplate = await _earthDB.PlayerBuildplates
                        .AsNoTracking()
                        .FirstOrDefaultAsync(buildplate => buildplate.Id == instanceInfo.BuildplateId && buildplate.AccountId == instanceInfo.PlayerId, cancellationToken);

                    if (buildplate is null)
                    {
                        return null;
                    }

                    size = buildplate.Size;
                    offset = buildplate.Offset;
                    scale = buildplate.Scale;
                }

                break;
            case Source.SHARED:
                {
                    var sharedBuildplate = await _earthDB.SharedBuildplates
                        .AsNoTracking()
                        .FirstOrDefaultAsync(sharedBuildplate => sharedBuildplate.Id == instanceInfo.BuildplateId, cancellationToken);

                    if (sharedBuildplate is null)
                    {
                        return null;
                    }

                    size = sharedBuildplate.Size;
                    offset = sharedBuildplate.Offset;
                    scale = sharedBuildplate.Scale;
                }

                break;
            case Source.ENCOUNTER:
                {
                    BuildplateGeometry? geometry = await GetEncounterBuildplateGeometry(instanceInfo.BuildplateId, cancellationToken);
                    if (geometry is null)
                    {
                        return null;
                    }

                    size = geometry.Size;
                    offset = geometry.Offset;
                    scale = geometry.Scale;
                }

                break;
            default:
                throw new UnreachableException();
        }

        return new BuildplateInstance(
            instanceInfo.InstanceId,
            Guid.Empty,
            "d.projectearth.dev",    // TODO
            instanceInfo.Address,
            instanceInfo.Port,
            instanceInfo.Ready,
            instanceInfo.Ready ? BuildplateInstance.ApplicationStatusE.READY : BuildplateInstance.ApplicationStatusE.UNKNOWN,
            instanceInfo.Ready ? BuildplateInstance.ServerStatusE.RUNNING : BuildplateInstance.ServerStatusE.RUNNING,
            Common.Json.Serialize(new Dictionary<string, object>()
            {
                { "buildplateid", instanceInfo.BuildplateId }
            }),
            new BuildplateInstance.GameplayMetadataR(
                instanceInfo.BuildplateId,
                Guid.Empty, // TODO - grab from buildplate
                instanceInfo.PlayerId,
                "2020.1217.02",
                "CK06Yzm2", // TODO
                new Dimension(size, size),
                new Offset(0, offset, 0),
                !fullsize ? scale : 1,
                fullsize,
                gameplayMode,
                SurfaceOrientation.HORIZONTAL,
                null,
                null, // TODO
                []
            ),
            "776932eeeb69",
            //new Coordinate(50.99636722700025f, -0.7234904312500047f)
            new Coordinate(0.0f, 0.0f)    // TODO
        );
    }

    private sealed record BuildplateGeometry(int Size, int Offset, int Scale);

    private async Task<BuildplateGeometry?> GetEncounterBuildplateGeometry(Guid buildplateId, CancellationToken cancellationToken)
    {
        var encounterBuildplate = await _earthDB.EncounterBuildplates
            .AsNoTracking()
            .FirstOrDefaultAsync(encounterBuildplate => encounterBuildplate.Id == buildplateId, cancellationToken);
        if (encounterBuildplate is not null)
        {
            return new BuildplateGeometry(encounterBuildplate.Size, encounterBuildplate.Offset, encounterBuildplate.Scale);
        }

        var templateBuildplate = await _earthDB.TemplateBuildplates
            .AsNoTracking()
            .FirstOrDefaultAsync(templateBuildplate => templateBuildplate.Id == buildplateId, cancellationToken);
        return templateBuildplate is null
            ? null
            : new BuildplateGeometry(templateBuildplate.Size, templateBuildplate.Offset, templateBuildplate.Scale);
    }

    private sealed record SharedBuildplateInstanceRequest(
        bool FullSize
    );
}
