using System.Diagnostics;
using System.Runtime.Versioning;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Solace.Buildplate.Connector.Model;
using Solace.Buildplate.Model;
using Solace.Common.Utils;
using Solace.EventBus.Client;

namespace Solace.Buildplate.Launcher;

internal sealed partial class InstanceManager : IAsyncDisposable
{
    private readonly Starter _starter;

    private Publisher? _publisher;
    private RequestSender? _requestSender;
    private RequestHandler? _requestHandler;

    private readonly Dictionary<Guid, ActiveInstance> _activeInstances = [];
    private bool _shuttingDown;

    private readonly ILogger<InstanceManager> _logger;
    private readonly Lock _lock = new();

    [JsonConverter(typeof(JsonStringEnumConverter<InstanceType>))]
    internal enum InstanceType
    {
        BUILD,
        PLAY,
        SHARED_BUILD,
        SHARED_PLAY,
        ENCOUNTER,
    }

    [JsonConverter(typeof(JsonStringEnumConverter<BuildplateSource>))]
    internal enum BuildplateSource
    {
        PLAYER,
        SHARED,
        ENCOUNTER,
    }

    internal sealed record StartRequest(
        Guid? PlayerId,
        Guid? EncounterId,
        Guid BuildplateId,
        bool Night,
        InstanceType Type,
        DateTimeOffset ShutdownTime
    );

    internal sealed record StartNotification(
        Guid InstanceId,
        Guid? PlayerId,
        Guid? EncounterId,
        Guid BuildplateId,
        string Address,
        int Port,
        InstanceType Type
    );

    internal sealed record BuildplateLoadRequest(
        Guid PlayerId,
        Guid BuildplateId
    );

    internal sealed record SharedBuildplateLoadRequest(
        Guid SharedBuildplateId
    );

    internal sealed record EncounterBuildplateLoadRequest(
        Guid EncounterBuildplateId
    );

    internal sealed record BuildplateLoadResponse(
        string ServerDataBase64
    );

    private sealed class ActiveInstance
    {
        public Guid InstanceId { get; init; }
        public Guid? PlayerId { get; init; }
        public Guid? EncounterId { get; init; }
        public Guid BuildplateId { get; init; }
        public InstanceType Type { get; init; }
        public bool Survival { get; init; }
        public bool Night { get; init; }
        public bool SaveEnabled { get; init; }
        public InventoryType InventoryType { get; init; }
        public DateTimeOffset? ShutdownTime { get; init; }
        public CancellationTokenSource? Cts { get; set; }
    }

    public InstanceManager(Starter starter, ILogger<InstanceManager> logger)
    {
        _starter = starter;
        _logger = logger;
    }

    [SupportedOSPlatform("android")]
    [SupportedOSPlatform("linux")]
    [SupportedOSPlatform("windows")]
    public async Task InitializeAsync(EventBusClient eventBusClient)
    {
        _publisher = await eventBusClient.AddPublisherAsync();
        _requestSender = await eventBusClient.AddRequestSenderAsync();

        LogStartingPersistentServer();
        await _starter.StartAsync();
        await _starter.WaitForServerReadyAsync();
        LogPersistentServerReady();

        _requestHandler = await eventBusClient.AddRequestHandlerAsync(Starter.BUILDPLATES_QUEUE_NAME,
            async (request, cancellationToken) =>
            {
                if (request.Type is "start")
                {
                    lock (_lock)
                    {
                        if (_shuttingDown)
                        {
                            return null;
                        }
                    }

                    StartRequest startRequest;
                    try
                    {
                        startRequest = JsonSerializer.Deserialize((string)request.Data.Value!, AppJsonContext.Default.StartRequest)!;
                    }
                    catch (Exception exception)
                    {
                        LogBadStartRequest(exception);
                        return null;
                    }

                    bool survival;
                    bool saveEnabled;
                    InventoryType inventoryType;
                    BuildplateSource buildplateSource;
                    DateTimeOffset? shutdownTime;
                    switch (startRequest.Type)
                    {
                        case InstanceType.BUILD:
                            survival = false;
                            saveEnabled = true;
                            inventoryType = InventoryType.SYNCED;
                            buildplateSource = BuildplateSource.PLAYER;
                            shutdownTime = null;
                            break;
                        case InstanceType.PLAY:
                            survival = true;
                            saveEnabled = false;
                            inventoryType = InventoryType.DISCARD;
                            buildplateSource = BuildplateSource.PLAYER;
                            shutdownTime = null;
                            break;
                        case InstanceType.SHARED_BUILD:
                            survival = false;
                            saveEnabled = false;
                            inventoryType = InventoryType.DISCARD;
                            buildplateSource = BuildplateSource.SHARED;
                            shutdownTime = null;
                            break;
                        case InstanceType.SHARED_PLAY:
                            survival = true;
                            saveEnabled = false;
                            inventoryType = InventoryType.DISCARD;
                            buildplateSource = BuildplateSource.SHARED;
                            shutdownTime = null;
                            break;
                        case InstanceType.ENCOUNTER:
                            survival = true;
                            saveEnabled = false;
                            inventoryType = InventoryType.BACKPACK;
                            buildplateSource = BuildplateSource.ENCOUNTER;
                            shutdownTime = startRequest.ShutdownTime;
                            break;
                        default:
                            LogBadStartRequest();
                            return null;
                    }

                    if (buildplateSource == BuildplateSource.PLAYER && startRequest.PlayerId is null)
                    {
                        LogBadStartRequest();
                        return null;
                    }

                    var instanceId = Guid.CreateVersion7();
                    LogStartingBuildplateInstance(instanceId);

                    var serverDataBase64 = await FetchServerDataAsync(buildplateSource, startRequest.PlayerId, startRequest.BuildplateId, cancellationToken);
                    if (serverDataBase64 is null)
                    {
                        LogErrorStartingBuildplateInstance(instanceId);
                        return null;
                    }

                    var createDimensionSuccess = await CreateDimensionAsync(instanceId, serverDataBase64, survival, startRequest.Night, saveEnabled, inventoryType, cancellationToken);
                    if (!createDimensionSuccess)
                    {
                        LogErrorStartingBuildplateInstance(instanceId);
                        return null;
                    }

                    var activeInstance = new ActiveInstance
                    {
                        InstanceId = instanceId,
                        PlayerId = startRequest.PlayerId,
                        EncounterId = startRequest.EncounterId,
                        BuildplateId = startRequest.BuildplateId,
                        Type = startRequest.Type,
                        Survival = survival,
                        Night = startRequest.Night,
                        SaveEnabled = saveEnabled,
                        InventoryType = inventoryType,
                        ShutdownTime = shutdownTime,
                    };

                    lock (_lock)
                    {
                        _activeInstances[instanceId] = activeInstance;
                    }

                    await SendEventBusMessageAsync("started", JsonSerializer.Serialize(new StartNotification(
                        instanceId,
                        startRequest.PlayerId,
                        startRequest.EncounterId,
                        startRequest.BuildplateId,
                        _starter.PublicAddress,
                        _starter.PublicPort,
                        startRequest.Type
                    ), AppJsonContext.Default.StartNotification), cancellationToken);

                    await SendEventBusMessageAsync("ready", instanceId.ToString(), cancellationToken);

                    if (shutdownTime is not null)
                    {
                        var cts = new CancellationTokenSource();
                        activeInstance.Cts = cts;
                        StartShutdownTimer(activeInstance, cts.Token);
                    }

                    return instanceId.ToString();
                }
                else if (request.Type is "preview")
                {
                    PreviewRequest previewRequest;
                    byte[] serverData;
                    try
                    {
                        previewRequest = JsonSerializer.Deserialize((string)request.Data.Value!, AppJsonContext.Default.PreviewRequest)!;
                        serverData = Convert.FromBase64String(previewRequest.ServerDataBase64);
                    }
                    catch (Exception exception)
                    {
                        LogBadPreviewRequest(exception);
                        return null;
                    }

                    LogGeneratingBuildplatePreview();
                    var preview = PreviewGenerator.GeneratePreview(serverData, previewRequest.Night, App.StaticDataPath.Value, _logger);
                    if (preview is null)
                    {
                        LogCouldNotGeneratePreviewForBuildplate();
                    }

                    return preview ?? (MessagePayload?)null;
                }
                else if (request.Type is "stop")
                {
                    if (!Guid.TryParse((string)request.Data.Value!, out var instanceId))
                    {
                        LogBadStopRequest();
                        return null;
                    }

                    LogStoppingBuildplateInstance(instanceId);
                    await UnloadInstanceAsync(instanceId, cancellationToken);
                    return "";
                }
                else
                {
                    return null;
                }
            },
            async exception =>
            {
                LogEventBusRequestHandlerError(exception);
            }
        );
    }

    private async Task<string?> FetchServerDataAsync(BuildplateSource source, Guid? playerId, Guid buildplateId, CancellationToken cancellationToken)
    {
        Debug.Assert(_requestSender is not null);

        try
        {
            string requestType;
            string requestPayload;
            switch (source)
            {
                case BuildplateSource.PLAYER:
                    requestType = "load";
                    requestPayload = JsonSerializer.Serialize(new BuildplateLoadRequest(playerId!.Value, buildplateId), AppJsonContext.Default.BuildplateLoadRequest);
                    break;
                case BuildplateSource.SHARED:
                    requestType = "loadShared";
                    requestPayload = JsonSerializer.Serialize(new SharedBuildplateLoadRequest(buildplateId), AppJsonContext.Default.SharedBuildplateLoadRequest);
                    break;
                case BuildplateSource.ENCOUNTER:
                    requestType = "loadEncounter";
                    requestPayload = JsonSerializer.Serialize(new EncounterBuildplateLoadRequest(buildplateId), AppJsonContext.Default.EncounterBuildplateLoadRequest);
                    break;
                default:
                    throw new UnreachableException();
            }

            var response = await _requestSender.RequestAsync(Starter.BUILDPLATES_QUEUE_NAME, requestType, requestPayload, cancellationToken);
            if (response is null)
            {
                return null;
            }

            var loadResponse = JsonSerializer.Deserialize((string)response.Value.Value!, AppJsonContext.Default.BuildplateLoadResponse);
            return loadResponse?.ServerDataBase64;
        }
        catch (Exception exception)
        {
            LogFetchServerDataError(exception, buildplateId);
            return null;
        }
    }

    private async Task<bool> CreateDimensionAsync(Guid instanceId, string serverDataBase64, bool survival, bool night, bool saveEnabled, InventoryType inventoryType, CancellationToken cancellationToken)
    {
        Debug.Assert(_requestSender is not null);

        try
        {
            var req = new CreateDimensionRequest(instanceId, serverDataBase64, survival, night, saveEnabled, inventoryType);
            var json = JsonSerializer.Serialize(req, AppJsonContext.Default.CreateDimensionRequest);

            var response = await _requestSender.RequestAsync(Starter.FABRIC_QUEUE_NAME, "createDimension", json, cancellationToken);
            if (response is null)
            {
                return false;
            }

            var result = JsonSerializer.Deserialize((string)response.Value.Value!, AppJsonContext.Default.Boolean);
            return result;
        }
        catch (Exception exception)
        {
            LogCreateDimensionError(exception, instanceId);
            return false;
        }
    }

    public async Task UnloadInstanceAsync(Guid instanceId, CancellationToken cancellationToken = default)
    {
        ActiveInstance? instance;
        lock (_lock)
        {
            if (!_activeInstances.Remove(instanceId, out instance))
            {
                return;
            }
        }

        instance.Cts?.Cancel();
        instance.Cts?.Dispose();

        await SendEventBusMessageAsync("shuttingDown", instanceId.ToString(), cancellationToken);

        if (_requestSender is not null)
        {
            try
            {
                var req = new UnloadDimensionRequest(instanceId, instance.SaveEnabled);
                var json = JsonSerializer.Serialize(req, AppJsonContext.Default.UnloadDimensionRequest);
                await _requestSender.RequestAsync(Starter.FABRIC_QUEUE_NAME, "unloadDimension", json, cancellationToken);
            }
            catch (Exception exception)
            {
                LogUnloadDimensionError(exception, instanceId);
            }
        }

        await SendEventBusMessageAsync("stopped", instanceId.ToString(), cancellationToken);
        LogBuildplateInstanceStopped(instanceId);
    }

    private void StartShutdownTimer(ActiveInstance activeInstance, CancellationToken cancellationToken)
        => _ = Task.Run(async () =>
        {
            if (activeInstance.ShutdownTime is { } shutdownTime)
            {
                var currentTime = DateTimeOffset.UtcNow;
                while (currentTime < shutdownTime && !cancellationToken.IsCancellationRequested)
                {
                    var duration = shutdownTime - currentTime;
                    if (duration > TimeSpan.Zero)
                    {
                        try
                        {
                            var delayMs = (int)double.Min(int.MaxValue, duration.TotalMilliseconds > 2000 ? duration.TotalMilliseconds / 2 : duration.TotalMilliseconds);
                            await Task.Delay(delayMs, cancellationToken);
                        }
                        catch (OperationCanceledException)
                        {
                            return;
                        }
                    }

                    currentTime = DateTimeOffset.UtcNow;
                }
            }

            if (!cancellationToken.IsCancellationRequested)
            {
                LogShutdownTimerDone(activeInstance.InstanceId);
                await UnloadInstanceAsync(activeInstance.InstanceId, CancellationToken.None);
            }
        }, cancellationToken);

    private async Task SendEventBusMessageAsync(string type, string message, CancellationToken cancellationToken = default)
    {
        if (_publisher is null)
        {
            return;
        }

        var result = await _publisher.PublishAsync(Starter.BUILDPLATES_QUEUE_NAME, type, message, cancellationToken);
        if (!result)
        {
            LogEventBusPublisherError();
        }
    }

    public async Task ShutdownAsync()
    {
        List<Guid> instanceIds;
        lock (_lock)
        {
            _shuttingDown = true;
            instanceIds = [.. _activeInstances.Keys];
        }

        LogShutdownSignalReceived(instanceIds.Count);

        foreach (var instanceId in instanceIds)
        {
            await UnloadInstanceAsync(instanceId, CancellationToken.None);
        }

        if (_requestHandler is not null)
        {
            await _requestHandler.DisposeAsync();
        }

        if (_publisher is not null)
        {
            await _publisher.DisposeAsync();
        }

        if (_requestSender is not null)
        {
            await _requestSender.DisposeAsync();
        }

        await _starter.DisposeAsync();
    }

    public ValueTask DisposeAsync()
        => new(ShutdownAsync());

    [LoggerMessage(Level = LogLevel.Information, Message = "Starting persistent server")]
    private partial void LogStartingPersistentServer();

    [LoggerMessage(Level = LogLevel.Information, Message = "Persistent server is ready")]
    private partial void LogPersistentServerReady();

    [LoggerMessage(Level = LogLevel.Error, Message = "Bad start request")]
    private partial void LogBadStartRequest();

    [LoggerMessage(Level = LogLevel.Error, Message = "Bad start request")]
    private partial void LogBadStartRequest(Exception exception);

    [LoggerMessage(Level = LogLevel.Information, Message = "Starting buildplate instance '{InstanceId}'")]
    private partial void LogStartingBuildplateInstance(Guid InstanceId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Error starting buildplate instance '{InstanceId}'")]
    private partial void LogErrorStartingBuildplateInstance(Guid InstanceId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Error fetching server data for buildplate '{BuildplateId}'")]
    private partial void LogFetchServerDataError(Exception exception, Guid BuildplateId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Error creating dimension for instance '{InstanceId}' on Fabric server")]
    private partial void LogCreateDimensionError(Exception exception, Guid InstanceId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Error unloading dimension for instance '{InstanceId}' on Fabric server")]
    private partial void LogUnloadDimensionError(Exception exception, Guid InstanceId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Buildplate instance '{InstanceId}' stopped")]
    private partial void LogBuildplateInstanceStopped(Guid InstanceId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Bad preview request")]
    private partial void LogBadPreviewRequest(Exception exception);

    [LoggerMessage(Level = LogLevel.Information, Message = "Generating buildplate preview")]
    private partial void LogGeneratingBuildplatePreview();

    [LoggerMessage(Level = LogLevel.Error, Message = "Could not generate preview for buildplate")]
    private partial void LogCouldNotGeneratePreviewForBuildplate();

    [LoggerMessage(Level = LogLevel.Error, Message = "Event bus request handler error")]
    private partial void LogEventBusRequestHandlerError(Exception? exception);

    [LoggerMessage(Level = LogLevel.Error, Message = "Event bus publisher error")]
    private partial void LogEventBusPublisherError();

    [LoggerMessage(Level = LogLevel.Information, Message = "Shutdown timer finished for instance '{InstanceId}'")]
    private partial void LogShutdownTimerDone(Guid InstanceId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Shutdown signal received, unloading {RunningInstanceCount} instances")]
    private partial void LogShutdownSignalReceived(int RunningInstanceCount);

    [LoggerMessage(Level = LogLevel.Information, Message = "Stopping buildplate instance '{InstanceId}'")]
    private partial void LogStoppingBuildplateInstance(Guid InstanceId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Bad stop request")]
    private partial void LogBadStopRequest();
}