using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using System.Text;
using System.Text.Json.Serialization;
using Solace.ApiServer.Types.Buildplates;
using Solace.ApiServer.Types.Common;
using Solace.ApiServer.Types.Inventory;
using Solace.ApiServer.Utils;
using Solace.Common.Utils;
using Solace.Db.Earth;
using Solace.Db.Earth.Models.Global;
using Solace.ObjectStore.Client;
using Solace.StaticData;
using Microsoft.EntityFrameworkCore;
using Solace.Common.Asp;
using Solace.EventBus.Client;

namespace Solace.ApiServer.Controllers;

[Authorize]
[ApiVersion("1.1")]
[Route("1/api/v{version:apiVersion}")]
internal sealed partial class BuildplatesController : SolaceControllerBase
{
    private readonly EarthDbContext _earthDb;
    private readonly EventBusClient _eventBus;
    private readonly ObjectStoreClient _objectStore;
    private readonly BuildplateInstancesManager _buildplateInstancesManager;
    private readonly StaticDataProvider _staticData;
    private readonly TappablesManager _tappablesManager;
    private readonly ILogger<BuildplatesController> _logger;

    public BuildplatesController(EarthDbContext earthDB, EventBusClient eventBus, ObjectStoreClient objectStore, BuildplateInstancesManager buildplateInstancesManager, StaticDataProvider staticData, TappablesManager tappablesManager, ILogger<BuildplatesController> logger)
    {
        _earthDb = earthDB;
        _eventBus = eventBus;
        _objectStore = objectStore;
        _buildplateInstancesManager = buildplateInstancesManager;
        _staticData = staticData;
        _tappablesManager = tappablesManager;
        _logger = logger;
    }

    [HttpGet("buildplates")]
    public async Task<Results<ContentHttpResult, NotFound, BadRequest>> GetBuildplates(CancellationToken cancellationToken)
    {
        if (!TryGetProfileId(out var profileId))
        {
            return TypedResults.BadRequest();
        }

        var CurrentProfileLevel = await _earthDb.Profiles
            .AsNoTracking()
            .Where(profile => profile.Id == profileId)
            .Select(profile => (int?)profile.Level)
            .FirstOrDefaultAsync(cancellationToken);

        if (CurrentProfileLevel is null)
        {
            return TypedResults.NotFound();
        }

        await LevelBuildplateSeeder.SeedLevelBuildplates(profileId, _earthDb, _eventBus, _objectStore, _staticData.Buildplates, _logger, cancellationToken);

        var buildplates = await _earthDb.PlayerBuildplates
            .AsNoTracking()
            .Where(buildplate => buildplate.ProfileId == profileId)
            .ToListAsync(cancellationToken);

        var templateIds = buildplates
            .Select(buildplate => buildplate.TemplateId)
            .WhereNotNull()
            .ToHashSet();

        var templates = await _earthDb.TemplateBuildplates
            .AsNoTracking()
            .Where(template => templateIds.Contains(template.Id))
            .Select(template => new { template.Id, template.RequiredLevel, template.Order, })
            .ToDictionaryAsync(template => template.Id, cancellationToken);

        var buildplateTasks = buildplates.Select(async buildplate =>
        {
            using var previewData = await _objectStore.GetStreamAsync(buildplate.PreviewObjectId, cancellationToken);
            if (previewData is null)
            {
                LogBuildplatePreviewNotFound(buildplate.PreviewObjectId, buildplate.Id);
                return null;
            }

            if (buildplate.TemplateId is null || !templates.TryGetValue(buildplate.TemplateId.Value, out var template))
            {
                template = null;
            }

            using var previewDataReader = new StreamReader(previewData, Encoding.ASCII);

            var model = await previewDataReader.ReadToEndAsync(cancellationToken);
            return new OwnedBuildplate(
                buildplate.Id.ToString(),
                "00000000-0000-0000-0000-000000000000",
                new Dimension(buildplate.Size, buildplate.Size),
                new Offset(0, buildplate.Offset, 0),
                buildplate.BlocksPerMeter,
                OwnedBuildplate.TypeE.SURVIVAL,
                SurfaceOrientation.HORIZONTAL,
                model,
                template?.Order ?? 0,
                CurrentProfileLevel < (template?.RequiredLevel ?? 1),
                template?.RequiredLevel ?? 1,
                false,    // TODO
                TimeFormatter.FormatTime(buildplate.LastModified),
                0,    // TODO
                ""
            );
        });

        var results = await Task.WhenAll(buildplateTasks);
        var ownedBuildplates = results.Where(b => b is not null).ToList()!;

        return EarthJson(ownedBuildplates);
    }

    [HttpPost("multiplayer/buildplate/{buildplateId}/instances")]
    public async Task<Results<ContentHttpResult, InternalServerError, NotFound, BadRequest>> CreateBuildInstance(Guid buildplateId, CancellationToken cancellationToken)
    {
        if (!TryGetProfileId(out var profileId))
        {
            return TypedResults.BadRequest();
        }

        // TODO: coordinates, etc.

        var buildplateInfo = await _earthDb.PlayerBuildplates
            .AsNoTracking()
            .Where(buildplate => buildplate.ProfileId == profileId && buildplate.Id == buildplateId)
            .Select(buildplate => new
            {
                RequiredLevel = buildplate.Template != null ? buildplate.Template.RequiredLevel : null,
                CurrentProfileLevel = (int?)buildplate.Profile.Level
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (buildplateInfo is null)
        {
            return TypedResults.NotFound();
        }

        if (buildplateInfo.RequiredLevel is > 1)
        {
            if (buildplateInfo.CurrentProfileLevel is null)
            {
                return TypedResults.NotFound();
            }

            if (buildplateInfo.CurrentProfileLevel < buildplateInfo.RequiredLevel.Value)
            {
                return TypedResults.BadRequest();
            }
        }

        return await GetNewBuildplateInstanceResponse(profileId, buildplateId, BuildplateInstancesManager.InstanceType.BUILD, cancellationToken);
    }

    [HttpPost("multiplayer/buildplate/{buildplateId}/play/instances")]
    public async Task<Results<ContentHttpResult, InternalServerError, NotFound, BadRequest>> CreatePlayInstance(Guid buildplateId, CancellationToken cancellationToken)
    {
        if (!TryGetProfileId(out var profileId))
        {
            return TypedResults.BadRequest();
        }

        // TODO: coordinates, etc.

        var buildplateInfo = await _earthDb.PlayerBuildplates
            .AsNoTracking()
            .Where(buildplate => buildplate.ProfileId == profileId && buildplate.Id == buildplateId)
            .Select(buildplate => new
            {
                RequiredLevel = buildplate.Template != null ? buildplate.Template.RequiredLevel : null,
                CurrentProfileLevel = (int?)buildplate.Profile.Level
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (buildplateInfo is null)
        {
            return TypedResults.NotFound();
        }

        if (buildplateInfo.RequiredLevel is > 1)
        {
            if (buildplateInfo.CurrentProfileLevel is null)
            {
                return TypedResults.NotFound();
            }

            if (buildplateInfo.CurrentProfileLevel < buildplateInfo.RequiredLevel.Value)
            {
                return TypedResults.BadRequest();
            }
        }

        return await GetNewBuildplateInstanceResponse(profileId, buildplateId, BuildplateInstancesManager.InstanceType.PLAY, cancellationToken);
    }

    [HttpPost("buildplates/{buildplateId}/share")]
    public async Task<Results<ContentHttpResult, BadRequest, NotFound, InternalServerError>> ShareBuildplate(Guid buildplateId, CancellationToken cancellationToken)
    {
        if (!TryGetProfileId(out var profileId))
        {
            return TypedResults.BadRequest();
        }

        var requestStartedOn = HttpContext.GetTimestamp();

        var buildplate = await _earthDb.PlayerBuildplates
            .AsNoTracking()
            .Where(buildplate => buildplate.Id == buildplateId && buildplate.ProfileId == profileId)
            .Select(buildplate => new
            {
                buildplate.Size,
                buildplate.Offset,
                buildplate.BlocksPerMeter,
                buildplate.Night,
                buildplate.LastModified,
                buildplate.ServerDataObjectId,
                RequiredLevel = buildplate.Template != null ? buildplate.Template.RequiredLevel : null,
                CurrentProfileLevel = (int?)buildplate.Profile.Level
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (buildplate is null)
        {
            return TypedResults.NotFound();
        }

        if (buildplate.RequiredLevel is > 1)
        {
            if (buildplate.CurrentProfileLevel is null)
            {
                return TypedResults.NotFound();
            }

            if (buildplate.CurrentProfileLevel < buildplate.RequiredLevel.Value)
            {
                return TypedResults.BadRequest();
            }
        }

        var hotbar = await _earthDb.Hotbars
            .AsNoTracking()
            .FirstAsync(hotbar => hotbar.Id == profileId, cancellationToken: cancellationToken);

        using var serverData = await _objectStore.GetStreamAsync(buildplate.ServerDataObjectId, cancellationToken);
        if (serverData is null)
        {
            LogBuildplateServerDataNotFound(buildplate.ServerDataObjectId, buildplateId);
            return TypedResults.InternalServerError();
        }

        var sharedBuildplateServerDataObjectId = await _objectStore.StoreAsync(serverData, cancellationToken);
        if (sharedBuildplateServerDataObjectId is null)
        {
            LogSharedBuildplateServerDataStoreError(buildplateId);
            return TypedResults.InternalServerError();
        }

        var sharedBuildplate = new SharedBuildplateEF()
        {
            ProfileId = profileId,
            Size = buildplate.Size,
            Offset = buildplate.Offset,
            BlocksPerMeter = buildplate.BlocksPerMeter,
            Night = buildplate.Night,
            Created = requestStartedOn,
            BuildplateLastModifed = buildplate.LastModified,
            ServerDataObjectId = sharedBuildplateServerDataObjectId.Value,
            LastViewed = requestStartedOn,
            NumberOfTimesViewed = 0,
        };

        for (var index = 0; index < 7; index++)
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
                var instance = await _earthDb.NonStackableItems
                    .AsNoTracking()
                    .FirstOrDefaultAsync(instance => instance.ProfileId == profileId && instance.ItemId == item.Uuid && instance.InstanceId == item.InstanceId.Value, cancellationToken);
                sharedBuildplateHotbarItem = new SharedBuildplateEF.HotbarItem(item.Uuid, 1, item.InstanceId, instance?.Wear ?? 0);
            }

            sharedBuildplate.Hotbar[index] = sharedBuildplateHotbarItem;
        }

        try
        {
            _earthDb.SharedBuildplates.Add(sharedBuildplate);
            await _earthDb.SaveChangesAsync(cancellationToken);
        }
        catch (Exception exception)
        {
            LogSharedBuildplateDBStoreError(exception, buildplateId);
            await _objectStore.DeleteAsync(sharedBuildplateServerDataObjectId.Value, cancellationToken);
            return TypedResults.InternalServerError();
        }

        return EarthJson($"minecraftearth://sharedbuildplate?id={sharedBuildplate.Id}");
    }

    [HttpGet("buildplates/shared/{sharedBuildplateId}")]
    public async Task<Results<ContentHttpResult, BadRequest, NotFound, InternalServerError>> GetSharedBuildplate(Guid sharedBuildplateId, CancellationToken cancellationToken)
    {
        var sharedBuildplate = await _earthDb.SharedBuildplates
            .AsNoTracking()
            .FirstOrDefaultAsync(sharedBuildplate => sharedBuildplate.Id == sharedBuildplateId, cancellationToken: cancellationToken);

        if (sharedBuildplate is null)
        {
            return TypedResults.NotFound();
        }

        var serverData = await _objectStore.GetArrayAsync(sharedBuildplate.ServerDataObjectId, cancellationToken);
        if (serverData is null)
        {
            LogSharedBuildplateServerDataNotFound(sharedBuildplate.ServerDataObjectId, sharedBuildplateId);
            return TypedResults.InternalServerError();
        }

        var preview = await _buildplateInstancesManager.GetBuildplatePreviewAsync(serverData, sharedBuildplate.Night, cancellationToken);
        if (preview is null)
        {
            LogSharedBuildplatePreviewGenerateError(sharedBuildplateId);
            return TypedResults.InternalServerError();
        }

        var ownerProfile = await _earthDb.Profiles
            .AsNoTracking()
            .Where(profile => profile.Id == sharedBuildplate.ProfileId)
            .Select(profile => new { profile.Username, })
            .FirstOrDefaultAsync(cancellationToken);

        return EarthJson(new SharedBuildplate(
            ownerProfile?.Username ?? sharedBuildplate.ProfileId.ToString(),
            TimeFormatter.FormatTime(sharedBuildplate.Created),
            new SharedBuildplate.BuildplateDataR(
                new Dimension(sharedBuildplate.Size, sharedBuildplate.Size),
                new Offset(0, sharedBuildplate.Offset, 0),
                sharedBuildplate.BlocksPerMeter,
                SharedBuildplate.BuildplateDataR.TypeE.SURVIVAL,
                SurfaceOrientation.HORIZONTAL,
                preview,
                0
            ),
            new Types.Inventory.InventoryResponse(
                [.. sharedBuildplate.Hotbar.Select(item => item is not null ? new HotbarItem(
                    item.Uuid,
                    item.Count,
                    item.InstanceId,
                    item.InstanceId is not null ? ItemWear.WearToHealth(item.Uuid, item.Wear, _staticData.Catalog.ItemsCatalog) : 0.0f
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
    public async Task<Results<ContentHttpResult, NotFound, BadRequest, InternalServerError>> CreateSharedBuildplateInstance(Guid sharedBuildplateId, CancellationToken cancellationToken)
    {
        if (!TryGetProfileId(out var profileId))
        {
            return TypedResults.BadRequest();
        }

        // TODO: coordinates etc.

        var sharedBuildplateInstanceRequest = (await Request.Body.AsJsonAsync(AppJsonContext.Default.SharedBuildplateInstanceRequest, cancellationToken))!;

        return await GetNewSharedBuildplateInstanceResponse(profileId, sharedBuildplateId, sharedBuildplateInstanceRequest.FullSize ? BuildplateInstancesManager.InstanceType.SHARED_PLAY : BuildplateInstancesManager.InstanceType.SHARED_BUILD, cancellationToken);
    }

    internal sealed record EncounterInstanceRequest(
        string TileId
    );

    [HttpPost("multiplayer/encounters/{encounterId}/instances")]
    public async Task<Results<ContentHttpResult, NotFound, BadRequest, InternalServerError>> CreateEncounterInstance(Guid encounterId, CancellationToken cancellationToken)
    {
        if (!TryGetProfileId(out _))
        {
            return TypedResults.BadRequest();
        }

        var encounterInstanceRequest = await Request.Body.AsJsonAsync(AppJsonContext.Default.EncounterInstanceRequest, cancellationToken);

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
        if (!TryGetProfileId(out var profileId))
        {
            return TypedResults.BadRequest();
        }

        var instanceInfo = _buildplateInstancesManager.GetInstanceInfo(instanceId);
        if (instanceInfo is null || instanceInfo.ShuttingDown)
        {
            return TypedResults.NotFound();
        }

        var buildplate = await _earthDb.PlayerBuildplates
            .AsNoTracking()
            .FirstOrDefaultAsync(buildplate => buildplate.Id == instanceInfo.BuildplateId && buildplate.ProfileId == profileId, cancellationToken: cancellationToken);

        if (buildplate is null)
        {
            return TypedResults.NotFound();
        }

        // TODO: the client is supposed to poll until the buildplate server is ready, but instead it just crashes if we tell it that the buildplate server is not ready yet
        // TODO: so instead we just stall the request until it's ready, this is really ugly and eventually we need to figure out why it's crashing and implement this properly
        // TODO: this also relies on the buildplate server starting in less than ~20 seconds as the client will eventually time out the HTTP request and crash anyway
        //BuildplateInstance buildplateInstance = this.instanceInfoToApiResponse(instanceInfo);
        BuildplateInstancesManager.InstanceInfo? instanceInfo1;
        var waitCount = 0;
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
        var buildplateInstance = await InstanceInfoToApiResponse(instanceInfo1, cancellationToken);

        if (buildplateInstance is null)
        {
            return TypedResults.NotFound();
        }

        return EarthJson(buildplateInstance);
    }

    private async Task<Results<ContentHttpResult, InternalServerError, NotFound, BadRequest>> GetNewBuildplateInstanceResponse(Guid profileId, Guid buildplateId, BuildplateInstancesManager.InstanceType type, CancellationToken cancellationToken)
    {
        var buildplate = await _earthDb.PlayerBuildplates
            .AsNoTracking()
            .FirstOrDefaultAsync(buildplate => buildplate.Id == buildplateId, cancellationToken);

        if (buildplate is null)
        {
            return TypedResults.NotFound();
        }

        var instanceId = await _buildplateInstancesManager.RequestBuildplateInstanceAsync(profileId, null, buildplateId, type, DateTimeOffset.MinValue, buildplate.Night, cancellationToken);
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

    private async Task<Results<ContentHttpResult, NotFound, BadRequest, InternalServerError>> GetNewSharedBuildplateInstanceResponse(Guid profileId, Guid sharedBuildplateId, BuildplateInstancesManager.InstanceType type, CancellationToken cancellationToken)
    {
        var sharedBuildplate = await _earthDb.SharedBuildplates
            .AsNoTracking()
            .FirstOrDefaultAsync(sharedBuildplate => sharedBuildplate.Id == sharedBuildplateId, cancellationToken);

        if (sharedBuildplate is null)
        {
            return TypedResults.NotFound();
        }

        var instanceId = await _buildplateInstancesManager.RequestBuildplateInstanceAsync(profileId, null, sharedBuildplateId, type, DateTimeOffset.MinValue, sharedBuildplate.Night, cancellationToken);
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

    private async Task<Results<ContentHttpResult, NotFound, BadRequest, InternalServerError>> GetNewEncounterBuildplateInstanceResponse(Guid encounterId, string tileIdStr, TappablesManager tappablesManager, CancellationToken cancellationToken)
    {
        if (!TappablesManager.TryParseTileId(tileIdStr, out var tileId))
        {
            return TypedResults.BadRequest();
        }

        var encounter = tappablesManager.GetEncounterWithId(encounterId, tileId);
        if (encounter is null)
        {
            return TypedResults.NotFound();
        }

        var instanceId = await _buildplateInstancesManager.RequestBuildplateInstanceAsync(null, encounterId, encounter.EncounterBuildplateId, BuildplateInstancesManager.InstanceType.ENCOUNTER, encounter.SpawnTime + encounter.ValidFor, false, cancellationToken);

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

    [JsonConverter(typeof(JsonStringEnumConverter<Source>))]
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

                    var buildplate = await _earthDb.PlayerBuildplates
                        .AsNoTracking()
                        .FirstOrDefaultAsync(buildplate => buildplate.Id == instanceInfo.BuildplateId && buildplate.ProfileId == instanceInfo.PlayerId, cancellationToken);

                    if (buildplate is null)
                    {
                        return null;
                    }

                    size = buildplate.Size;
                    offset = buildplate.Offset;
                    scale = buildplate.BlocksPerMeter;
                }

                break;
            case Source.SHARED:
                {
                    var sharedBuildplate = await _earthDb.SharedBuildplates
                        .AsNoTracking()
                        .FirstOrDefaultAsync(sharedBuildplate => sharedBuildplate.Id == instanceInfo.BuildplateId, cancellationToken);

                    if (sharedBuildplate is null)
                    {
                        return null;
                    }

                    size = sharedBuildplate.Size;
                    offset = sharedBuildplate.Offset;
                    scale = sharedBuildplate.BlocksPerMeter;
                }

                break;
            case Source.ENCOUNTER:
                {
                    var geometry = await GetEncounterBuildplateGeometry(instanceInfo.BuildplateId, cancellationToken);
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
#pragma warning disable MA0140 // Both if and else branch have identical code
            instanceInfo.Ready ? BuildplateInstance.ServerStatusE.RUNNING : BuildplateInstance.ServerStatusE.RUNNING,
#pragma warning restore MA0140 // Both if and else branch have identical code
            Common.Json.Serialize(new Dictionary<string, object>(StringComparer.Ordinal)
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
                [with(StringComparer.Ordinal)]
            ),
            "776932eeeb69",
            //new Coordinate(50.99636722700025f, -0.7234904312500047f)
            new Coordinate(0.0f, 0.0f)    // TODO
        );
    }

    private sealed record BuildplateGeometry(int Size, int Offset, int Scale);

    private async Task<BuildplateGeometry?> GetEncounterBuildplateGeometry(Guid buildplateId, CancellationToken cancellationToken)
    {
        var encounterBuildplate = await _earthDb.EncounterBuildplates
            .AsNoTracking()
            .FirstOrDefaultAsync(encounterBuildplate => encounterBuildplate.Id == buildplateId, cancellationToken);
        if (encounterBuildplate is not null)
        {
            return new BuildplateGeometry(encounterBuildplate.Size, encounterBuildplate.Offset, encounterBuildplate.BlocksPerMeter);
        }

        var templateBuildplate = await _earthDb.TemplateBuildplates
            .AsNoTracking()
            .FirstOrDefaultAsync(templateBuildplate => templateBuildplate.Id == buildplateId, cancellationToken);
        return templateBuildplate is null
            ? null
            : new BuildplateGeometry(templateBuildplate.Size, templateBuildplate.Offset, templateBuildplate.BlocksPerMeter);
    }

    internal sealed record SharedBuildplateInstanceRequest(
        bool FullSize
    );

    [LoggerMessage(Level = LogLevel.Error, Message = "Preview object {PreviewObjectId} for buildplate {BuildplateId} could not be loaded from object store")]
    private partial void LogBuildplatePreviewNotFound(Guid PreviewObjectId, Guid BuildplateId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Data object {ServerDataObjectId} for buildplate {BuildplateId} could not be loaded from object store")]
    private partial void LogBuildplateServerDataNotFound(Guid ServerDataObjectId, Guid BuildplateId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Could not store data object for shared buildplate '{BuildplateId}' in object store")]
    private partial void LogSharedBuildplateServerDataStoreError(Guid BuildplateId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to store shared buildplate '{BuildplateId}' to db")]
    private partial void LogSharedBuildplateDBStoreError(Exception ex, Guid BuildplateId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Data object {ServerDataObjectId} for shared buildplate {BuildplateId} could not be loaded from object store")]
    private partial void LogSharedBuildplateServerDataNotFound(Guid ServerDataObjectId, Guid BuildplateId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Could not get preview for shared buildplate '{BuildplateId}'")]
    private partial void LogSharedBuildplatePreviewGenerateError(Guid BuildplateId);

}
