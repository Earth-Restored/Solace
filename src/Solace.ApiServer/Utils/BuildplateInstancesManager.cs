using Serilog;
using System.Text.Json.Serialization;
using Solace.Common;
using Solace.Common.Utils;
using Solace.EventBus.Client;

namespace Solace.ApiServer.Utils;

public sealed class BuildplateInstancesManager
{
    private readonly EventBusClient _eventBusClient;
    private Subscriber _subscriber = null!;
    private RequestSender _requestSender = null!;

    private readonly Dictionary<Guid, TaskCompletionSource<bool>?> _pendingInstances = [];
    private readonly Dictionary<Guid, InstanceInfo> _instances = [];
    private readonly Dictionary<Guid, HashSet<Guid>> _instancesByBuildplateId = [];

    private BuildplateInstancesManager(EventBusClient eventBusClient)
    {
        _eventBusClient = eventBusClient;
    }

    public static async Task<BuildplateInstancesManager> CreateAsync(EventBusClient eventBusClient)
    {
        var buildplateInstancesManager = new BuildplateInstancesManager(eventBusClient);

        buildplateInstancesManager._subscriber = await eventBusClient.AddSubscriberAsync("buildplates", new SubscriberListener(
           buildplateInstancesManager.HandleEvent,
           async () =>
           {
               Log.Fatal("Buildplates event bus subscriber error");
               Log.CloseAndFlush();
               Environment.Exit(1);
           }
       ));

        buildplateInstancesManager._requestSender = await eventBusClient.AddRequestSenderAsync();

        return buildplateInstancesManager;
    }

    public async Task<Guid?> RequestBuildplateInstance(Guid? playerId, Guid? encounterId, Guid buildplateId, InstanceType type, long shutdownTime, bool night)
    {
        if (playerId is null && type is not InstanceType.ENCOUNTER)
        {
            throw new ArgumentException($"{nameof(playerId)} cannot be null when {nameof(type)} is not {nameof(InstanceType.ENCOUNTER)}.");
        }

        if (encounterId is not null && type is not InstanceType.ENCOUNTER)
        {
            throw new ArgumentException($"{nameof(encounterId)} can only be set when {nameof(type)} is {nameof(InstanceType.ENCOUNTER)}.");
        }

        if (playerId is not null && encounterId is not null)
        {
            Log.Information($"Finding buildplate instance for buildplate {buildplateId} type {type} encounter {encounterId} player {playerId}");
        }
        else if (playerId is not null)
        {
            Log.Information($"Finding buildplate instance for buildplate {buildplateId} type {type} player {playerId}");
        }
        else if (encounterId is not null)
        {
            Log.Information($"Finding buildplate instance for buildplate {buildplateId} type {type} encounter {encounterId}");
        }
        else
        {
            Log.Information($"Finding buildplate instance for buildplate {buildplateId} type {type}");
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
                            Log.Information($"Found existing buildplate instance {loopInstanceId}");
                            return loopInstanceId;
                        }
                    }
                }
            }
        }

        Log.Information("Did not find existing instance, starting new instance");
        string? instanceIdString = await _requestSender.RequestAsync("buildplates", "start", Json.Serialize(new StartRequest(playerId, encounterId, buildplateId, night, type, shutdownTime)));
        if (!Guid.TryParse(instanceIdString, out var instanceId))
        {
            Log.Error("Buildplate start request was rejected/ignored");
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
            Log.Warning($"Could not start buildplate instance {instanceId}");
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
        Log.Information("Requesting buildplate preview");

        string? preview = await _requestSender.RequestAsync("buildplates", "preview", Json.Serialize(new PreviewRequest(Convert.ToBase64String(serverData), night)));
        if (preview is null)
        {
            Log.Error("Preview request was rejected/ignored");
        }

        return preview;
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
                            Log.Warning("Bad start notification");
                            return Task.CompletedTask;
                        }

                        lock (_instances)
                        {
                            Log.Information($"Buildplate instance {startNotification.InstanceId} has started");
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
                    catch (Exception ex)
                    {
                        Log.Warning($"Bad start notification: {ex}");
                    }
                }

                break;
            case "ready":
                {
                    if (!Guid.TryParse(@event.Data, out var instanceId))
                    {
                        Log.Warning($"Failed to parse instance guid for 'ready': '{@event.Data}'");
                        break;
                    }

                    lock (_instances)
                    {
                        InstanceInfo? instanceInfo = _instances.GetValueOrDefault(instanceId);
                        if (instanceInfo is not null)
                        {
                            Log.Information($"Buildplate instance {instanceId} is ready");
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
                        Log.Warning($"Failed to parse instance guid for 'shuttingDown': '{@event.Data}'");
                        break;
                    }

                    lock (_instances)
                    {
                        InstanceInfo? instanceInfo = _instances.GetValueOrDefault(instanceId);
                        if (instanceInfo is not null)
                        {
                            Log.Information($"Buildplate instance {instanceId} is shutting down");
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
                        Log.Warning($"Failed to parse instance guid for 'stopped': '{@event.Data}'");
                        break;
                    }

                    lock (_instances)
                    {
                        if (_instances.Remove(instanceId, out var instanceInfo))
                        {
                            Log.Information($"Buildplate instance {instanceId} has stopped");

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
        long ShutdownTime
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

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum InstanceType
    {
#pragma warning disable CA1707 // Identifiers should not contain underscores
        BUILD,
        PLAY,
        SHARED_BUILD,
        SHARED_PLAY,
        ENCOUNTER,
#pragma warning restore CA1707 // Identifiers should not contain underscores
    }

    public sealed record InstanceInfo(
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
}
