using Serilog;
using System.Text.Json.Serialization;
using Solace.Common;
using Solace.Common.Utils;
using Solace.EventBus.Client;

namespace Solace.ApiServer.Utils;

public sealed class BuildplateInstancesManager
{
    private readonly EventBusClient _eventBusClient;
    private readonly SemaphoreSlim _requestSenderLock = new(1, 1);
    private readonly SemaphoreSlim _startLock = new(1, 1);
    private Subscriber _subscriber = null!;
    private RequestSender _requestSender = null!;

    private readonly Dictionary<string, TaskCompletionSource<bool>?> _pendingInstances = [];
    private readonly Dictionary<string, InstanceInfo> _instances = [];
    private readonly Dictionary<string, HashSet<string>> _instancesByBuildplateId = [];

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

    public async Task<string?> RequestBuildplateInstance(string? playerId, string? encounterId, string buildplateId, InstanceType type, long shutdownTime, bool night)
    {
        if (playerId is null && type is not InstanceType.ENCOUNTER and not InstanceType.PLAYER_ADVENTURE)
        {
            throw new ArgumentException($"{nameof(playerId)} cannot be null when {nameof(type)} is not {nameof(InstanceType.ENCOUNTER)} or {nameof(InstanceType.PLAYER_ADVENTURE)}.");
        }

        if (encounterId is not null && type is not InstanceType.ENCOUNTER and not InstanceType.PLAYER_ADVENTURE)
        {
            throw new ArgumentException($"{nameof(encounterId)} can only be set when {nameof(type)} is {nameof(InstanceType.ENCOUNTER)} or {nameof(InstanceType.PLAYER_ADVENTURE)}.");
        }

        await _startLock.WaitAsync();
        try
        {
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
            HashSet<string>? instanceIds = _instancesByBuildplateId.GetOrDefault(buildplateId);
            if (instanceIds is not null)
            {
                foreach (string loopInstanceId in instanceIds)
                {
                    InstanceInfo? instanceInfo = _instances.GetOrDefault(loopInstanceId);
                    if (instanceInfo is not null && !instanceInfo.ShuttingDown)
                    {
                        bool sameEncounter = instanceInfo.EncounterId == encounterId || type is InstanceType.PLAYER_ADVENTURE;
                        if (instanceInfo.Type == type &&
                            instanceInfo.PlayerId == playerId &&
                            sameEncounter
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
        string? instanceId = await SendStartRequestWithTimeoutAsync(playerId, encounterId, buildplateId, night, type, shutdownTime);
        if (instanceId is null)
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
        finally
        {
            _startLock.Release();
        }
    }

    private async Task<string?> SendStartRequestWithTimeoutAsync(string? playerId, string? encounterId, string buildplateId, bool night, InstanceType type, long shutdownTime)
    {
        await _requestSenderLock.WaitAsync();
        try
        {
            Task<string?> responseTask = _requestSender.RequestAsync("buildplates", "start", Json.Serialize(new StartRequest(playerId, encounterId, buildplateId, night, type, shutdownTime)));
            Task timeoutTask = Task.Delay(TimeSpan.FromSeconds(8));
            if (await Task.WhenAny(responseTask, timeoutTask) == responseTask)
            {
                return await responseTask;
            }

            Log.Warning("Buildplate start request timed out for buildplate {BuildplateId}; resetting sender", buildplateId);
            await ResetRequestSenderAsync();
            return null;
        }
        finally
        {
            _requestSenderLock.Release();
        }
    }

    private async Task ResetRequestSenderAsync()
    {
        try
        {
            await _requestSender.CloseAsync();
        }
        catch (Exception exception)
        {
            Log.Warning(exception, "Could not close stale buildplates request sender");
        }

        _requestSender = await _eventBusClient.AddRequestSenderAsync();
    }

    public InstanceInfo? GetInstanceInfo(string instanceId)
    {
        lock (_instances)
        {
            return _instances.GetOrDefault(instanceId, null);
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
                        if (startNotification.PlayerId is null && startNotification.Type is not InstanceType.ENCOUNTER and not InstanceType.PLAYER_ADVENTURE)
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
                            TaskCompletionSource<bool>? completableFuture = _pendingInstances.JavaRemove(startNotification.InstanceId);
                            completableFuture?.SetResult(true);
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
                    string instanceId = @event.Data;
                    lock (_instances)
                    {
                        InstanceInfo? instanceInfo = _instances.GetOrDefault(instanceId, null);
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
                    string instanceId = @event.Data;
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
                    string instanceId = @event.Data;
                    lock (_instances)
                    {
                        var instanceInfo = _instances.JavaRemove(instanceId);
                        if (instanceInfo is not null)
                        {
                            Log.Information($"Buildplate instance {instanceId} has stopped");

                            var instanceIds = _instancesByBuildplateId.GetOrDefault(instanceInfo.BuildplateId);
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
        string? PlayerId,
        string? EncounterId,
        string BuildplateId,
        bool Night,
        InstanceType Type,
        long ShutdownTime
    );

    private sealed record PreviewRequest(
        string ServerDataBase64,
        bool Night
    );

    private sealed record StartNotification(
        string InstanceId,
        string? PlayerId,
        string? EncounterId,
        string BuildplateId,
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
        PLAYER_ADVENTURE,
#pragma warning restore CA1707 // Identifiers should not contain underscores
    }

    public sealed record InstanceInfo(
        InstanceType Type,

        string InstanceId,

        string? PlayerId,
        string? EncounterId,
        string BuildplateId,

        string Address,
        int Port,

        bool Ready,
        bool ShuttingDown
    );
}
