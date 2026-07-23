using System.Diagnostics;
using System.Text.Json.Serialization;
using Solace.Common;
using Solace.Common.Utils;
using Solace.EventBus.Client;

namespace Solace.ApiServer.Utils;

internal sealed partial class BuildplateInstancesManager : IAsyncDisposable
{
    private Subscriber? _subscriber;
    private RequestSender? _requestSender;

    private readonly ILogger<BuildplateInstancesManager> _logger;

    private readonly Dictionary<Guid, TaskCompletionSource<bool>?> _pendingInstances = [];
    private readonly Dictionary<Guid, InstanceInfo> _instances = [];
    private readonly Dictionary<Guid, HashSet<Guid>> _instancesByBuildplateId = [];

    public BuildplateInstancesManager(ILogger<BuildplateInstancesManager> logger)
    {
        _logger = logger;
    }

    internal async Task InitializeAsync(EventBusClient eventBusClient)
    {
        _subscriber = await eventBusClient.AddSubscriberAsync("buildplates",
            HandleEvent,
            async exception =>
            {
                LogBuildplatesEventBusSubscriberError(exception);
                Console.Error.WriteLine(exception);
                Console.Error.Flush();
                Environment.Exit(1);
            }
        );

        _requestSender = await eventBusClient.AddRequestSenderAsync();
    }

    public async Task<Guid?> RequestBuildplateInstance(Guid? playerId, Guid? encounterId, Guid buildplateId, InstanceType type, DateTimeOffset shutdownTime, bool night)
    {
        if (playerId is null && type is not InstanceType.ENCOUNTER)
        {
#pragma warning disable MA0015 // Specify the parameter name in ArgumentException
            throw new ArgumentException($"{nameof(playerId)} cannot be null when {nameof(type)} is not {nameof(InstanceType.ENCOUNTER)}.");
        }

        if (encounterId is not null && type is not InstanceType.ENCOUNTER)
        {
            throw new ArgumentException($"{nameof(encounterId)} can only be set when {nameof(type)} is {nameof(InstanceType.ENCOUNTER)}.");
#pragma warning restore MA0015 // Specify the parameter name in ArgumentException
        }

        if (playerId is not null && encounterId is not null)
        {
            LogFindingBuildplateInstanceForBuildplateEncounterPlayer(buildplateId, type, encounterId.Value, playerId.Value);
        }
        else if (playerId is not null)
        {
            LogFindingBuildplateInstanceForBuildplatePlayer(buildplateId, type, playerId.Value);
        }
        else if (encounterId is not null)
        {
            LogFindingBuildplateInstanceForBuildplateEncounter(buildplateId, type, encounterId.Value);
        }
        else
        {
            LogFindingBuildplateInstanceForBuildplate(buildplateId, type);
        }

        lock (_instances)
        {
            var instanceIds = _instancesByBuildplateId.GetValueOrDefault(buildplateId);
            if (instanceIds is not null)
            {
                foreach (var loopInstanceId in instanceIds)
                {
                    var instanceInfo = _instances.GetValueOrDefault(loopInstanceId);
                    if (instanceInfo is not null && !instanceInfo.ShuttingDown)
                    {
                        if (instanceInfo.Type == type &&
                            instanceInfo.PlayerId == playerId &&
                            instanceInfo.EncounterId == encounterId
                        )
                        {
                            LogFoundExistingBuildplateInstance(loopInstanceId);
                            return loopInstanceId;
                        }
                    }
                }
            }
        }

        LogStartingNewInstance();

        Debug.Assert(_requestSender is not null);

        var instanceIdString = await _requestSender.RequestAsync("buildplates", "start", Json.Serialize(new StartRequest(playerId, encounterId, buildplateId, night, type, shutdownTime)));
        if (!Guid.TryParse(instanceIdString, out var instanceId))
        {
            LogBuildplateStartRequestWasRejectedIgnored();
            return null;
        }

        TaskCompletionSource<bool> completableFuture = new();
        lock (_instances)
        {
            if (_instances.ContainsKey(instanceId))
            {
                completableFuture.SetResult(true);
            }
            else
            {
                lock (_pendingInstances)
                {
                    _pendingInstances[instanceId] = completableFuture;
                }
            }
        }

        if (!await completableFuture.Task)
        {
            LogCouldNotStartBuildplateInstance(instanceId);
            return null;
        }

        return instanceId;
    }

    public InstanceInfo? GetInstanceInfo(Guid instanceId)
    {
        lock (_instances)
        {
            return _instances.GetValueOrDefault(instanceId);
        }
    }

    public async Task<string?> GetBuildplatePreviewAsync(byte[] serverData, bool night)
    {
        LogRequestingBuildplatePreview();

        Debug.Assert(_requestSender is not null);

        var preview = await _requestSender.RequestAsync("buildplates", "preview", Json.Serialize(new PreviewRequest(Convert.ToBase64String(serverData), night)));
        if (preview is null)
        {
            LogPreviewRequestWasRejectedIgnored();
        }

        return preview;
    }

    public async ValueTask DisposeAsync()
    {
        if (_subscriber is not null)
        {
            await _subscriber.DisposeAsync();
        }

        if (_requestSender is not null)
        {
            await _requestSender.DisposeAsync();
        }
    }

    private Task HandleEvent(SubscriberEvent @event)
    {
        switch (@event.Type)
        {
            case "started":
                {
                    StartNotification startNotification;
                    try
                    {
                        startNotification = Json.Deserialize<StartNotification>(@event.Data)!;
                        if (startNotification.PlayerId is null && startNotification.Type is not InstanceType.ENCOUNTER)
                        {
                            // Log.Warning("Bad start notification");
                            return Task.CompletedTask;
                        }

                        lock (_instances)
                        {
                            LogBuildplateInstanceHasStarted(startNotification.InstanceId);
                            _instances[startNotification.InstanceId] = new InstanceInfo(
                                startNotification.Type,
                                startNotification.InstanceId,
                                startNotification.PlayerId,
                                startNotification.EncounterId,
                                startNotification.BuildplateId,
                                startNotification.Address,
                                startNotification.Port,
                                false,
                                false
                            );

                            _instancesByBuildplateId.ComputeIfAbsent(startNotification.BuildplateId, buildplateId => [])!.Add(startNotification.InstanceId);
                        }

                        lock (_pendingInstances)
                        {
                            if (_pendingInstances.Remove(startNotification.InstanceId, out var completableFuture))
                            {
                                completableFuture?.SetResult(true);
                            }
                        }
                    }
                    catch (Exception exception)
                    {
                        LogBadStartNotification(exception);
                    }
                }

                break;
            case "ready":
                {
                    if (!Guid.TryParse(@event.Data, out var instanceId))
                    {
                        // Log.Warning($"Failed to parse instance guid for 'ready': '{@event.Data}'");
                        break;
                    }

                    lock (_instances)
                    {
                        InstanceInfo? instanceInfo = _instances.GetValueOrDefault(instanceId);
                        if (instanceInfo is not null)
                        {
                            LogBuildplateInstanceIsReady(instanceId);
                            _instances[instanceId] = new InstanceInfo(
                                instanceInfo.Type,
                                instanceInfo.InstanceId,
                                instanceInfo.PlayerId,
                                instanceInfo.EncounterId,
                                instanceInfo.BuildplateId,
                                instanceInfo.Address,
                                instanceInfo.Port,
                                true,
                                instanceInfo.ShuttingDown
                            );
                        }
                    }
                }

                break;
            case "shuttingDown":
                {
                    if (!Guid.TryParse(@event.Data, out var instanceId))
                    {
                        // Log.Warning($"Failed to parse instance guid for 'shuttingDown': '{@event.Data}'");
                        break;
                    }

                    lock (_instances)
                    {
                        InstanceInfo? instanceInfo = _instances.GetValueOrDefault(instanceId);
                        if (instanceInfo is not null)
                        {
                            LogBuildplateInstanceIsShuttingDown(instanceId);
                            _instances[instanceId] = new InstanceInfo(
                                instanceInfo.Type,
                                instanceInfo.InstanceId,
                                instanceInfo.PlayerId,
                                instanceInfo.EncounterId,
                                instanceInfo.BuildplateId,
                                instanceInfo.Address,
                                instanceInfo.Port,
                                instanceInfo.Ready,
                                true
                            );
                        }
                    }
                }

                break;
            case "stopped":
                {
                    if (!Guid.TryParse(@event.Data, out var instanceId))
                    {
                        // Log.Warning($"Failed to parse instance guid for 'stopped': '{@event.Data}'");
                        break;
                    }

                    lock (_instances)
                    {
                        if (_instances.Remove(instanceId, out var instanceInfo))
                        {
                            LogBuildplateInstancHasStopped(instanceId);

                            var instanceIds = _instancesByBuildplateId.GetValueOrDefault(instanceInfo.BuildplateId);
                            instanceIds?.Remove(instanceInfo.InstanceId);
                        }
                    }
                }

                break;
            default:
                break;
        }

        return Task.CompletedTask;
    }

    private sealed record StartRequest(
        Guid? PlayerId,
        Guid? EncounterId,
        Guid BuildplateId,
        bool Night,
        InstanceType Type,
        DateTimeOffset ShutdownTime
    );

    private sealed record PreviewRequest(
        string ServerDataBase64,
        bool Night
    );

    private sealed record StartNotification(
        Guid InstanceId,
        Guid? PlayerId,
        Guid? EncounterId,
        Guid BuildplateId,
        string Address,
        int Port,
        InstanceType Type
    );

    [JsonConverter(typeof(JsonStringEnumConverter<InstanceType>))]
    internal enum InstanceType
    {
#pragma warning disable CA1707 // Identifiers should not contain underscores
        BUILD,
        PLAY,
        SHARED_BUILD,
        SHARED_PLAY,
        ENCOUNTER,
#pragma warning restore CA1707 // Identifiers should not contain underscores
    }

    internal sealed record InstanceInfo(
        InstanceType Type,

        Guid InstanceId,

        Guid? PlayerId,
        Guid? EncounterId,
        Guid BuildplateId,

        string Address,
        int Port,

        bool Ready,
        bool ShuttingDown
    );

    [LoggerMessage(Level = LogLevel.Critical, Message = "Buildplates event bus subscriber error")]
    private partial void LogBuildplatesEventBusSubscriberError(Exception? exception);

    [LoggerMessage(Level = LogLevel.Information, Message = "Finding buildplate instance for buildplate {BuildplateId} type {Type} encounter {EncounterId} player {AccountId}")]
    private partial void LogFindingBuildplateInstanceForBuildplateEncounterPlayer(Guid BuildplateId, InstanceType Type, Guid EncounterId, Guid AccountId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Finding buildplate instance for buildplate {BuildplateId} type {Type} player {AccountId}")]
    private partial void LogFindingBuildplateInstanceForBuildplatePlayer(Guid BuildplateId, InstanceType Type, Guid AccountId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Finding buildplate instance for buildplate {BuildplateId} type {Type} encounter {EncounterId}")]
    private partial void LogFindingBuildplateInstanceForBuildplateEncounter(Guid BuildplateId, InstanceType Type, Guid EncounterId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Finding buildplate instance for buildplate {BuildplateId} type {Type}")]
    private partial void LogFindingBuildplateInstanceForBuildplate(Guid BuildplateId, InstanceType Type);

    [LoggerMessage(Level = LogLevel.Information, Message = "Found existing buildplate instance {InstanceId}")]
    private partial void LogFoundExistingBuildplateInstance(Guid InstanceId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Did not find existing instance, starting new instance")]
    private partial void LogStartingNewInstance();

    [LoggerMessage(Level = LogLevel.Error, Message = "Buildplate start request was rejected/ignored")]
    private partial void LogBuildplateStartRequestWasRejectedIgnored();

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not start buildplate instance {InstanceId}")]
    private partial void LogCouldNotStartBuildplateInstance(Guid InstanceId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Requesting buildplate preview")]
    private partial void LogRequestingBuildplatePreview();

    [LoggerMessage(Level = LogLevel.Error, Message = "Preview request was rejected/ignored")]
    private partial void LogPreviewRequestWasRejectedIgnored();

    [LoggerMessage(Level = LogLevel.Information, Message = "Buildplate instance {InstanceId} has started")]
    private partial void LogBuildplateInstanceHasStarted(Guid InstanceId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Bad start notification")]
    private partial void LogBadStartNotification(Exception exception);

    [LoggerMessage(Level = LogLevel.Information, Message = "Buildplate instance {InstanceId} is ready")]
    private partial void LogBuildplateInstanceIsReady(Guid InstanceId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Buildplate instance {InstanceId} is shutting down")]
    private partial void LogBuildplateInstanceIsShuttingDown(Guid InstanceId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Buildplate instance {InstanceId} has stopped")]
    private partial void LogBuildplateInstancHasStopped(Guid InstanceId);
}
