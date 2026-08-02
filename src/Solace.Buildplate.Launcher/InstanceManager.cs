using System.Diagnostics;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Solace.Buildplate.Connector.Model;
using Solace.Buildplate.Model;
using Solace.Common;
using Solace.Common.Utils;
using Solace.EventBus.Client;

namespace Solace.Buildplate.Launcher;

internal sealed partial class InstanceManager
{
    private readonly Starter _starter;

    private Publisher? _publisher;
    private RequestHandler? _requestHandler;
    private int _runningInstanceCount;
    private bool _shuttingDown;

    private readonly ILogger<InstanceManager> _logger;

    private readonly Lock _lock = new Lock();

    [JsonConverter(typeof(JsonStringEnumConverter<InstanceType>))]
    private enum InstanceType
    {
        BUILD,
        PLAY,
        SHARED_BUILD,
        SHARED_PLAY,
        ENCOUNTER,
    }

    private sealed record StartRequest(
        Guid? PlayerId,
        Guid? EncounterId,
        Guid BuildplateId,
        bool Night,
        InstanceType Type,
        DateTimeOffset ShutdownTime
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

    public InstanceManager(Starter starter, ILogger<InstanceManager> logger)
    {
        _starter = starter;

        _logger = logger;
    }

    public async Task InitializeAsync(EventBusClient eventBusClient)
    {
        _publisher = await eventBusClient.AddPublisherAsync();

        _requestHandler = await eventBusClient.AddRequestHandlerAsync("buildplates",
            async (request, cancellationToken) =>
            {
                if (request.Type is "start")
                {
                    _lock.Enter();
                    if (_shuttingDown)
                    {
                        _lock.Exit();
                        return null;
                    }

                    _runningInstanceCount += 1;
                    _lock.Exit();

                    StartRequest startRequest;
                    try
                    {
                        startRequest = Json.Deserialize<StartRequest>(request.Data)!;
                    }
                    catch (Exception exception)
                    {
                        LogBadStartRequest(exception);
                        return null;
                    }

                    bool survival;
                    bool saveEnabled;
                    InventoryType inventoryType;
                    Instance.BuildplateSource buildplateSource;
                    DateTimeOffset? shutdownTime;
                    switch (startRequest.Type)
                    {
                        case InstanceType.BUILD:
                            {
                                survival = false;
                                saveEnabled = true;
                                inventoryType = InventoryType.SYNCED;
                                buildplateSource = Instance.BuildplateSource.PLAYER;
                                shutdownTime = null;
                            }

                            break;
                        case InstanceType.PLAY:
                            {
                                survival = true;
                                saveEnabled = false;
                                inventoryType = InventoryType.DISCARD;
                                buildplateSource = Instance.BuildplateSource.PLAYER;
                                shutdownTime = null;
                            }

                            break;
                        case InstanceType.SHARED_BUILD:
                            {
                                survival = false;
                                saveEnabled = false;
                                inventoryType = InventoryType.DISCARD;
                                buildplateSource = Instance.BuildplateSource.SHARED;
                                shutdownTime = null;
                            }

                            break;
                        case InstanceType.SHARED_PLAY:
                            {
                                survival = true;
                                saveEnabled = false;
                                inventoryType = InventoryType.DISCARD;
                                buildplateSource = Instance.BuildplateSource.SHARED;
                                shutdownTime = null;
                            }

                            break;
                        case InstanceType.ENCOUNTER:
                            {
                                survival = true;
                                saveEnabled = false;
                                inventoryType = InventoryType.BACKPACK;
                                buildplateSource = Instance.BuildplateSource.ENCOUNTER;
                                shutdownTime = startRequest.ShutdownTime;
                            }

                            break;
                        default:
                            {
                                LogBadStartRequest();
                                return null;
                            }
                    }

                    if (buildplateSource == Instance.BuildplateSource.PLAYER && startRequest.PlayerId is null)
                    {
                        LogBadStartRequest();
                        return null;
                    }

                    var instanceId = Guid.CreateVersion7();

                    LogStartingBuildplateInstance(instanceId);

                    var instance = _starter.StartInstance(instanceId, startRequest.PlayerId, startRequest.BuildplateId, buildplateSource, survival, startRequest.Night, saveEnabled, inventoryType, shutdownTime);
                    if (instance is null)
                    {
                        LogErrorStartingBuildplateInstance(instanceId);
                        return null;
                    }

                    SendEventBusMessage("started", Json.Serialize(new StartNotification(
                        instanceId,
                        startRequest.PlayerId,
                        startRequest.EncounterId,
                        startRequest.BuildplateId,
                        instance.PublicAddress,
                        instance.Port,
                        startRequest.Type
                    )), cancellationToken);

                    Task.Run(async () =>
                    {
                        try
                        {
                            await instance.WaitForShutdownAsync();

                            SendEventBusMessage("stopped", instance.InstanceId.ToString());
                        }
                        catch (Exception exception)
                        {
                            LogFailedToSendStoppedMessage(exception);
                        }

                        _lock.Enter();
                        _runningInstanceCount -= 1;
                        _lock.Exit();
                    }, CancellationToken.None).Forget();

                    return instanceId.ToString();
                }
                else if (request.Type is "preview")
                {
                    PreviewRequest previewRequest;
                    byte[] serverData;
                    try
                    {
                        previewRequest = Json.Deserialize<PreviewRequest>(request.Data)!;
                        serverData = Convert.FromBase64String(previewRequest.ServerDataBase64);
                    }
                    catch (Exception exception)
                    {
                        LogBadPreviewRequest(exception);
                        return null;
                    }

                    LogGeneratingBuildplatePreview();

                    var preview = PreviewGenerator.GeneratePreview(serverData, previewRequest.Night, App.StaticDataPath, _logger);
                    if (preview is null)
                    {
                        LogCouldNotGeneratePreviewForBuildplate();
                    }

                    return preview;
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

    private void SendEventBusMessage(string type, string message, CancellationToken cancellationToken = default)
    {
        Debug.Assert(_publisher is not null);

        _publisher.PublishAsync("buildplates", type, message, cancellationToken)
            .ContinueWith(task =>
            {
                if (!task.Result)
                {
                    LogEventBusPublisherError();
                }
            }, cancellationToken)
            .Forget();
    }

    public async Task ShutdownAsync()
    {
        if (_requestHandler is not null)
        {
            await _requestHandler.DisposeAsync();
        }

        _lock.Enter();
        _shuttingDown = true;
        LogShutdownSignalReceived(_runningInstanceCount);
        while (_runningInstanceCount > 0)
        {
            var runningInstanceCount = _runningInstanceCount;
            _lock.Exit();

            await Task.Delay(1000);

            _lock.Enter();
            if (_runningInstanceCount != runningInstanceCount)
            {
                LogWaitingForInstancesToFinish(runningInstanceCount);
            }
        }

        _lock.Exit();

        if (_publisher is not null)
        {
            await _publisher.DisposeAsync();
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Bad start request")]
    private partial void LogBadStartRequest();

    [LoggerMessage(Level = LogLevel.Error, Message = "Bad start request")]
    private partial void LogBadStartRequest(Exception exception);

    [LoggerMessage(Level = LogLevel.Information, Message = "Starting buildplate instance '{InstanceId}'")]
    private partial void LogStartingBuildplateInstance(Guid InstanceId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Error starting buildplate instance '{InstanceId}'")]
    private partial void LogErrorStartingBuildplateInstance(Guid InstanceId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to send stopped message")]
    private partial void LogFailedToSendStoppedMessage(Exception exception);

    [LoggerMessage(Level = LogLevel.Error, Message = "Bad preview request")]
    private  partial void LogBadPreviewRequest(Exception exception);

    [LoggerMessage(Level = LogLevel.Information, Message = "Generating buildplate preview")]
    private partial void LogGeneratingBuildplatePreview();

    [LoggerMessage(Level = LogLevel.Error, Message = "Could not generate preview for buildplate")]
    private partial void LogCouldNotGeneratePreviewForBuildplate();

    [LoggerMessage(Level = LogLevel.Error, Message = "Event bus request handler error")]
    private partial void LogEventBusRequestHandlerError(Exception? exception);

    [LoggerMessage(Level = LogLevel.Error, Message = "Event bus publisher error")]
    private partial void LogEventBusPublisherError();

    [LoggerMessage(Level = LogLevel.Information, Message = "Shutdown signal received, no new buildplate instances will be started, waiting for {RunningInstanceCount} instances to finish")]
    private partial void LogShutdownSignalReceived(int RunningInstanceCount);

    [LoggerMessage(Level = LogLevel.Information, Message = "Waiting for {RunningInstanceCount} instances to finish")]
    private partial void LogWaitingForInstancesToFinish(int RunningInstanceCount);
}