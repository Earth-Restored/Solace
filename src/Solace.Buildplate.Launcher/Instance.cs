using System.Diagnostics;
using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.Json.Serialization;
using Cyotek.Data.Nbt;
using Cyotek.Data.Nbt.Serialization;
using Microsoft.Extensions.Logging;
using Solace.Buildplate.Connector.Model;
using Solace.Common;
using Solace.Common.Utils;
using Solace.EventBus.Client;

namespace Solace.Buildplate.Launcher;

#pragma warning disable CA1001 // Types that own disposable fields should be disposable
internal sealed partial class Instance
#pragma warning restore CA1001 // Types that own disposable fields should be disposable
{
    private const long HOST_PLAYER_CONNECT_TIMEOUT = 120_000;

    public static Instance Run(EventBusClient eventBusClient, Guid? playerId, Guid buildplateId, BuildplateSource buildplateSource, Guid instanceId, bool survival, bool night, bool saveEnabled, InventoryType inventoryType, DateTimeOffset? shutdownTime, string publicAddress, int port, int serverInternalPort, string javaCmd, FileInfo fountainBridgeJar, DirectoryInfo serverTemplateDir, string fabricJarName, FileInfo connectorPluginJar, DirectoryInfo baseDir, string eventBusConnectionString, ILoggerFactory loggerFactory, ILogger logger)
    {
        if (playerId is null && buildplateSource is BuildplateSource.PLAYER)
        {
#pragma warning disable MA0015 // Specify the parameter name in ArgumentException
            throw new ArgumentException($"{nameof(playerId)} cannot be null when {nameof(buildplateSource)} is {nameof(BuildplateSource.PLAYER)}");
#pragma warning restore MA0015 // Specify the parameter name in ArgumentException
        }

        var instance = new Instance(eventBusClient, playerId, buildplateId, buildplateSource, instanceId, survival, night, saveEnabled, inventoryType, shutdownTime, publicAddress, port, serverInternalPort, javaCmd, fountainBridgeJar, serverTemplateDir, fabricJarName, connectorPluginJar, baseDir, eventBusConnectionString, loggerFactory, logger);
        instance._threadStartedSemaphore.Wait();
        instance._thread = instance.RunAsync();
        instance._threadStartedSemaphore.Wait();
        instance._threadStartedSemaphore.Release();
        return instance;
    }

    private readonly EventBusClient _eventBusClient;

    private readonly Guid? _playerId;
    private readonly Guid _buildplateId;
    private readonly BuildplateSource _buildplateSource;
    public readonly Guid InstanceId;
    private readonly bool _survival;
    private readonly bool _night;
    private readonly bool _saveEnabled;
    private readonly InventoryType _inventoryType;
    private readonly DateTimeOffset? _shutdownTime;

    public readonly string PublicAddress;
    public readonly int Port;
    private readonly int _serverInternalPort;

    private readonly string _javaCmd;
    private readonly FileInfo _fountainBridgeJar;
    private readonly DirectoryInfo _serverTemplateDir;
    private readonly string _fabricJarName;
    private readonly FileInfo _connectorPluginJar;
    private readonly DirectoryInfo _baseDir;
    private readonly string _eventBusAddress;
    private readonly string _eventBusQueueName;
    private readonly string _connectorPluginArgString;

    private readonly ILoggerFactory _loggerFactory;

    private Task? _thread;
    private readonly SemaphoreSlim _threadStartedSemaphore = new SemaphoreSlim(1, 1);
    private readonly ILogger _logger;

    private Publisher? _publisher;
    private RequestSender? _requestSender;

    private Subscriber? _subscriber;
    private RequestHandler? _requestHandler;

    private DirectoryInfo _serverWorkDir = null!;
    private DirectoryInfo _bridgeWorkDir = null!;
    private ConsoleProcess? _serverProcess;
    private ConsoleProcess? _bridgeProcess;
    private bool _shuttingDown;
    private readonly ReentrantAsyncLock.ReentrantAsyncLock _subprocessLock = new ReentrantAsyncLock.ReentrantAsyncLock(); // java uses ReentrantLock, Lock cannot be used, because it does not support locking and unlocking on different threads, which happens due to async, SemaphoreSlim does not support multiple locks from the same async context

    private volatile bool _hostPlayerConnected;

    private Instance(EventBusClient eventBusClient, Guid? playerId, Guid buildplateId, BuildplateSource buildplateSource, Guid instanceId, bool survival, bool night, bool saveEnabled, InventoryType inventoryType, DateTimeOffset? shutdownTime, string publicAddress, int port, int serverInternalPort, string javaCmd, FileInfo fountainBridgeJar, DirectoryInfo serverTemplateDir, string fabricJarName, FileInfo connectorPluginJar, DirectoryInfo baseDir, string eventBusConnectionString, ILoggerFactory loggerFactory, ILogger logger)
    {
        _eventBusClient = eventBusClient;

        _playerId = playerId;
        _buildplateId = buildplateId;
        _buildplateSource = buildplateSource;
        InstanceId = instanceId;
        _survival = survival;
        _night = night;
        _saveEnabled = saveEnabled;
        _inventoryType = inventoryType;
        _shutdownTime = shutdownTime;

        PublicAddress = publicAddress;
        Port = port;
        _serverInternalPort = serverInternalPort;

        _loggerFactory = loggerFactory;

        _javaCmd = javaCmd;
        _fountainBridgeJar = fountainBridgeJar;
        _serverTemplateDir = serverTemplateDir;
        _fabricJarName = fabricJarName;
        _connectorPluginJar = connectorPluginJar;
        _baseDir = baseDir;
        _eventBusAddress = eventBusConnectionString;
        _eventBusQueueName = "buildplate_" + InstanceId;
        _connectorPluginArgString = Json.Serialize(new ConnectorPluginArg(
            _eventBusAddress,
            _eventBusQueueName,
            _inventoryType
        ));

        _logger = logger;
    }

    private async Task RunAsync()
    {
        await Task.Yield();

        _threadStartedSemaphore.Release();

        try
        {
            switch (_buildplateSource)
            {
                case BuildplateSource.PLAYER:
                    LogStartingForPlayer(_playerId, _buildplateId, _survival, _saveEnabled, _inventoryType);
                    break;
                case BuildplateSource.SHARED:
                    LogStartingForSharedBuildplate(_buildplateId, _playerId, _survival, _saveEnabled, _inventoryType);
                    break;
                case BuildplateSource.ENCOUNTER:
                    LogStartingForEncounterBuildplate(_buildplateId, _playerId, _survival, _saveEnabled, _inventoryType);
                    break;
            }

            LogPortUsageInfo(Port, _serverInternalPort);

            _publisher = await _eventBusClient.AddPublisherAsync();
            _requestSender = await _eventBusClient.AddRequestSenderAsync();

            LogSettingUpServer();

            BuildplateLoadResponse? buildplateLoadResponse = _buildplateSource switch
            {
                BuildplateSource.PLAYER => await SendEventBusRequestRaw<BuildplateLoadResponse>("load", new BuildplateLoadRequest(_playerId!.Value, _buildplateId), true),
                BuildplateSource.SHARED => await SendEventBusRequestRaw<BuildplateLoadResponse>("loadShared", new SharedBuildplateLoadRequest(_buildplateId), true),
                BuildplateSource.ENCOUNTER => await SendEventBusRequestRaw<BuildplateLoadResponse>("loadEncounter", new EncounterBuildplateLoadRequest(_buildplateId), true),
                _ => throw new UnreachableException(),
            };

            Debug.Assert(buildplateLoadResponse is not null);

            byte[] serverData;
            try
            {
                serverData = Convert.FromBase64String(buildplateLoadResponse.ServerDataBase64);
            }
            catch (Exception exception)
            {
                LogBuildplateLoadInvalidBase64(exception);
                return;
            }

            try
            {
                var serverWorkDir = await SetupServerFiles(serverData);
                if (serverWorkDir is null)
                {
                    LogSetupServerFilesError();
                    return;
                }

                _serverWorkDir = serverWorkDir;
            }
            catch (IOException exception)
            {
                LogSetupServerFilesError(exception);
                return;
            }

            try
            {
                var bridgeWorkDir = SetupBridgeFiles(serverData);
                if (bridgeWorkDir is null)
                {
                    LogSetupBridgeFilesError();
                    return;
                }

                _bridgeWorkDir = bridgeWorkDir;
            }
            catch (IOException exception)
            {
                LogSetupBridgeFilesError(exception);
                return;
            }

            LogRunningServer();

            _subscriber = await _eventBusClient.AddSubscriberAsync(_eventBusQueueName,
                HandleConnectorEvent,
                async exception =>
                {
                    LogEventBusSubscriberError(exception);
                    BeginShutdown();
                }
            );

            _requestHandler = await _eventBusClient.AddRequestHandlerAsync(_eventBusQueueName,
                async (request, cancellationToken) =>
                {
                    var responseObject = await HandleConnectorRequestAsync(request, cancellationToken);
                    return responseObject is not null ? Json.Serialize(responseObject) : null;
                },
                async exception =>
                {
                    LogEventBusRequestHandlerError(exception);
                    BeginShutdown();
                }
            );

            var @lock = await _subprocessLock.LockAsync(CancellationToken.None);

            if (!_shuttingDown)
            {
                await StartServerProcessAsync();

                if (_serverProcess is not null)
                {
                    await @lock.DisposeAsync();
                    await _serverProcess.WaitForExitAsync();
                    @lock = await _subprocessLock.LockAsync(CancellationToken.None);
                    var exitCode = _serverProcess.ExitCodeText;
                    _serverProcess.Dispose();
                    _serverProcess = null;
                    if (!_shuttingDown)
                    {
                        LogServerProcessUnexpectedExit(exitCode);
                    }
                    else
                    {
                        LogServerProcessFinished(exitCode);
                    }

                    _shuttingDown = true;

                    if (_bridgeProcess is not null)
                    {
                        LogWaitingForBridge();
                        await @lock.DisposeAsync();
                        await _bridgeProcess.StopAndWaitAsync(_logger);
                        exitCode = _bridgeProcess.ExitCodeText;
                        @lock = await _subprocessLock.LockAsync(CancellationToken.None);
                        _bridgeProcess.Dispose();
                        _bridgeProcess = null;
                        LogBridgeProcessFinished(exitCode);
                    }
                }
                else
                {
                    LogServerStartFailed();
                }
            }

            await @lock.DisposeAsync();
        }
        catch (Exception exception)
        {
            LogUnhandledException(exception);
        }
        finally
        {
            if (_subscriber is not null)
            {
                await _subscriber.DisposeAsync();
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

            CleanupBaseDir();

            _serverProcess?.Dispose();
            _bridgeProcess?.Dispose();

            LogInstanceFinished();
        }
    }

    private async Task HandleConnectorEvent(SubscriberEvent @event, CancellationToken cancellationToken)
    {
        switch (@event.Type)
        {
            case "started":
                {
                    LogServerIsReady();
                    await StartBridgeProcessAsync();
                    SendEventBusInstanceStatusNotification("ready", cancellationToken);
                    if (_shutdownTime is not null)
                    {
                        StartShutdownTimer();
                    }
                    else
                    {
                        StartHostPlayerConnectTimeout();
                    }
                }

                break;
            case "saved":
                {
                    if (_saveEnabled)
                    {
                        WorldSavedMessage? worldSavedMessage = ReadJson<WorldSavedMessage>(@event.Data);
                        if (worldSavedMessage is not null)
                        {
                            if (_hostPlayerConnected)
                            {
                                LogSavingSnapshot();
                                SendEventBusRequest<object>("saved", worldSavedMessage, false, cancellationToken)
                                    .Forget();
                            }
                            else
                            {
                                LogNotSavingSnapshotHostNotConnected();
                            }
                        }
                    }
                    // else
                    // {
                    //     _logger.LogDebug("Ignoring save data because saving is disabled");
                    // }
                }

                break;
            case "inventoryAdd":
                {
                    InventoryAddItemMessage? inventoryAddItemMessage = ReadJson<InventoryAddItemMessage>(@event.Data);
                    if (inventoryAddItemMessage is not null)
                    {
                        SendEventBusRequest<object>("inventoryAdd", inventoryAddItemMessage, false, cancellationToken)
                            .Forget();
                    }
                }

                break;
            case "inventoryUpdateWear":
                {
                    InventoryUpdateItemWearMessage? inventoryUpdateItemWearMessage = ReadJson<InventoryUpdateItemWearMessage>(@event.Data);
                    if (inventoryUpdateItemWearMessage is not null)
                    {
                        SendEventBusRequest<object>("inventoryUpdateWear", inventoryUpdateItemWearMessage, false, cancellationToken)
                            .Forget();
                    }
                }

                break;

            case "inventorySetHotbar":
                {
                    InventorySetHotbarMessage? inventorySetHotbarMessage = ReadJson<InventorySetHotbarMessage>(@event.Data);
                    if (inventorySetHotbarMessage is not null)
                    {
                        SendEventBusRequest<object>("inventorySetHotbar", inventorySetHotbarMessage, false, cancellationToken)
                            .Forget();
                    }
                }

                break;
        }
    }

    private async Task<object?> HandleConnectorRequestAsync(RequestHandlerRequest request, CancellationToken cancellationToken)
    {
        switch (request.Type)
        {
            case "playerConnected":
                {
                    PlayerConnectedRequest? playerConnectedRequest = ReadJson<PlayerConnectedRequest>(request.Data);
                    if (playerConnectedRequest is not null)
                    {
                        if (_playerId is not null && !_hostPlayerConnected && playerConnectedRequest.Uuid != _playerId)
                        {
                            LogRejectingPlayerConnectionHostNotFirst(playerConnectedRequest.Uuid);
                            return new PlayerConnectedResponse(false, null);
                        }

                        PlayerConnectedResponse? playerConnectedResponse = await SendEventBusRequest<PlayerConnectedResponse>("playerConnected", playerConnectedRequest, true, cancellationToken);
                        if (playerConnectedResponse is not null)
                        {
                            LogPlayerConnected(playerConnectedRequest.Uuid);

                            if (_playerId is not null && !_hostPlayerConnected && playerConnectedRequest.Uuid == _playerId)
                            {
                                _hostPlayerConnected = true;
                            }

                            return playerConnectedResponse;
                        }
                        else
                        {
                            // Log.Debug("[playerConnected] invalid api response");
                        }
                    }
                    else
                    {
                        // Log.Debug("[playerConnected] failed to read json");
                    }
                }

                break;
            case "playerDisconnected":
                {
                    PlayerDisconnectedRequest? playerDisconnectedRequest = ReadJson<PlayerDisconnectedRequest>(request.Data);
                    if (playerDisconnectedRequest is not null)
                    {
                        PlayerDisconnectedResponse? playerDisconnectedResponse = await SendEventBusRequest<PlayerDisconnectedResponse>("playerDisconnected", playerDisconnectedRequest, true, cancellationToken);
                        if (playerDisconnectedResponse is not null)
                        {
                            LogPlayerDisconnected(playerDisconnectedRequest.PlayerId);

                            if (_shutdownTime is null && _playerId is not null && playerDisconnectedRequest.PlayerId == _playerId)
                            {
                                LogHostPlayerDisconnected();
                                BeginShutdown();
                            }

                            return playerDisconnectedResponse;
                        }
                    }
                }

                break;
            case "playerDead":
                {
                    var playerId = ReadJson<string>(request.Data);
                    if (playerId is not null)
                    {
                        var respawn = await SendEventBusRequest<bool?>("playerDead", playerId, true, cancellationToken);
                        if (respawn is not null)
                        {
                            return respawn.Value;
                        }
                    }
                }

                break;
            case "getInventory":
                {
                    var playerId = ReadJson<string>(request.Data);
                    if (playerId is not null)
                    {
                        InventoryResponse? inventoryResponse = await SendEventBusRequest<InventoryResponse>("getInventory", playerId, true, cancellationToken);
                        if (inventoryResponse is not null)
                        {
                            return inventoryResponse;
                        }
                        else
                        {
                            // Log.Debug("[getInventory] invalid api response");
                        }
                    }
                    else
                    {
                        // Log.Debug("[getInventory] failed to read json");
                    }
                }

                break;
            case "inventoryRemove":
                {
                    InventoryRemoveItemRequest? inventoryRemoveItemRequest = ReadJson<InventoryRemoveItemRequest>(request.Data);
                    if (inventoryRemoveItemRequest is not null)
                    {
                        if (inventoryRemoveItemRequest.InstanceId is not null)
                        {
                            var success = await SendEventBusRequest<bool?>("inventoryRemove", inventoryRemoveItemRequest, true, cancellationToken);
                            if (success is not null)
                            {
                                return success.Value;
                            }
                        }
                        else
                        {
                            var removedCount = await SendEventBusRequest<int?>("inventoryRemove", inventoryRemoveItemRequest, true, cancellationToken);
                            if (removedCount is not null)
                            {
                                return removedCount.Value;
                            }
                        }
                    }
                }

                break;
            case "findPlayer":
                {
                    FindPlayerIdRequest? findPlayerIdRequest = ReadJson<FindPlayerIdRequest>(request.Data);
                    if (findPlayerIdRequest is not null)
                    {
                        // TODO
                        return findPlayerIdRequest.MinecraftName;
                    }
                    else
                    {
                        // Log.Debug("[findPlayer] failed to read json");
                    }
                }

                break;
            case "getInitialPlayerState":
                {
                    var playerId = ReadJson<string>(request.Data);
                    if (playerId is not null)
                    {
                        InitialPlayerStateResponse? initialPlayerStateResponse = await SendEventBusRequest<InitialPlayerStateResponse>("getInitialPlayerState", playerId, true, cancellationToken);
                        if (initialPlayerStateResponse is not null)
                        {
                            return initialPlayerStateResponse;
                        }
                    }
                    else
                    {
                        // Log.Debug("[getInitialPlayerState] failed to read json");
                    }
                }

                break;
        }

        return null;
    }

    private T? ReadJson<T>(string str)
    {
        try
        {
            return Json.Deserialize<T>(str);
        }
        catch (Exception exception)
        {
            LogEventBusDecodeError(exception);
            BeginShutdown();
            return default;
        }
    }

    private void SendEventBusInstanceStatusNotification(string status, CancellationToken cancellationToken = default)
    {
        Debug.Assert(_publisher is not null);

        _publisher.PublishAsync("buildplates", status, InstanceId.ToString(), cancellationToken)
            .ContinueWith(task =>
            {
                if (!task.Result)
                {
                    LogEventBusPublisherError();
                    BeginShutdown();
                }
            }, cancellationToken)
            .Forget();
    }

    private sealed record RequestWithInstanceId(
        string InstanceId,
        object Request
    );

    private Task<T?> SendEventBusRequest<T>(string type, object obj, bool returnResponse, CancellationToken cancellationToken = default)
    {
        var request = new RequestWithInstanceId(InstanceId.ToString(), obj);

        return SendEventBusRequestRaw<T>(type, request, returnResponse, cancellationToken);
    }

    private async Task<T?> SendEventBusRequestRaw<T>(string type, object obj, bool returnResponse, CancellationToken cancellationToken = default)
    {
        Debug.Assert(_requestSender is not null);

        try
        {
            var response = await _requestSender.RequestAsync("buildplates", type, Json.Serialize(obj), cancellationToken);

            if (response is null)
            {
                LogEventBusRequestNoResponse();
                BeginShutdown();
                return default;
            }

            if (returnResponse)
            {
                Debug.Assert(typeof(T) != typeof(object));
                return Json.Deserialize<T>(response);
            }
            else
            {
                Debug.Assert(typeof(T) == typeof(object));
                return default;
            }
        }
        catch (Exception exception)
        {
            LogEventBusRequestFail(exception);
            BeginShutdown();
            return default;
        }
    }

    private async Task<DirectoryInfo?> SetupServerFiles(byte[] serverData)
    {
        var workDir = new DirectoryInfo(Path.Combine(_baseDir.FullName, "server"));
        try
        {
            workDir.Create();
        }
        catch (IOException exception)
        {
            LogServerDirectoryCreateFail(exception);
            return null;
        }

        if (!CopyServerFile(new FileInfo(Path.Combine(_serverTemplateDir.FullName, _fabricJarName)), new FileInfo(Path.Combine(workDir.FullName, _fabricJarName)), false))
        {
            LogFabricJarNotFound(_fabricJarName);
            return null;
        }

        var warnedMissingServerFiles = false;
        if (!CopyServerFile(new DirectoryInfo(Path.Combine(_serverTemplateDir.FullName, ".fabric", "server")), new DirectoryInfo(Path.Combine(workDir.FullName, ".fabric", "server")), true))
        {
            if (!warnedMissingServerFiles)
            {
                LogServerFilesNotPreDownloaded();
                warnedMissingServerFiles = true;
            }
        }

        if (!CopyServerFile(new DirectoryInfo(Path.Combine(_serverTemplateDir.FullName, "libraries")), new DirectoryInfo(Path.Combine(workDir.FullName, "libraries")), true))
        {
            if (!warnedMissingServerFiles)
            {
                LogServerFilesNotPreDownloaded();
                warnedMissingServerFiles = true;
            }
        }

        if (!CopyServerFile(new DirectoryInfo(Path.Combine(_serverTemplateDir.FullName, "versions")), new DirectoryInfo(Path.Combine(workDir.FullName, "versions")), true))
        {
            if (!warnedMissingServerFiles)
            {
                LogServerFilesNotPreDownloaded();
#pragma warning disable IDE0059 // Unnecessary assignment of a value
                warnedMissingServerFiles = true;
#pragma warning restore IDE0059 // Unnecessary assignment of a value
            }
        }

        if (!CopyServerFile(new DirectoryInfo(Path.Combine(_serverTemplateDir.FullName, "mods")), new DirectoryInfo(Path.Combine(workDir.FullName, "mods")), true))
        {
            LogModsDirrectoryNotFound();
        }

        await File.WriteAllTextAsync(Path.Combine(workDir.FullName, "eula.txt"), "eula=true");

        var serverProperties = new StringBuilder()
            .Append("online-mode=false\n")
            .Append("enforce-secure-profile=false\n")
            .Append("sync-chunk-writes=false\n")
            .Append("spawn-protection=0\n")
            .Append("enable-command-block=true\n")
            .Append(CultureInfo.InvariantCulture, $"server-port={_serverInternalPort.ToString(CultureInfo.InvariantCulture)}\n")
            .Append(CultureInfo.InvariantCulture, $"gamemode={(_survival ? "survival" : "creative")}\n")
            .Append(CultureInfo.InvariantCulture, $"vienna-event-bus-address={_eventBusAddress}\n")
            .Append(CultureInfo.InvariantCulture, $"vienna-event-bus-queue-name={_eventBusQueueName}\n")
            .ToString();
        await File.WriteAllTextAsync(Path.Combine(workDir.FullName, "server.properties"), serverProperties);

        var worldDir = new DirectoryInfo(Path.Combine(workDir.FullName, "world"));
        try
        {
            workDir.Create();
        }
        catch (IOException exception)
        {
            LogServerWorldDirectoryCreateFail(exception);
            return null;
        }

        var worldEntitiesDir = new DirectoryInfo(Path.Combine(worldDir.FullName, "entities"));
        try
        {
            worldEntitiesDir.Create();
        }
        catch (IOException exception)
        {
            LogServerWorldEntitiesDirectoryCreateFail(exception);
            return null;
        }

        var worldRegionDir = new DirectoryInfo(Path.Combine(worldDir.FullName, "region"));
        try
        {
            worldRegionDir.Create();
        }
        catch (IOException exception)
        {
            LogServerWorldRegionsDirectoryCreateFail(exception);
            return null;
        }

        TagCompound levelDatTag = CreateLevelDat(_survival, _night);
        using (var fs = new FileStream(Path.Combine(worldDir.FullName, "level.dat"), FileMode.OpenOrCreate, FileAccess.Write, FileShare.Read))
        using (var gzs = new GZipStream(fs, CompressionLevel.Optimal))
        {
            var writer = new BinaryTagWriter(gzs);
            writer.WriteStartDocument();
            writer.WriteStartTag(null, TagType.Compound);
            writer.WriteTag(levelDatTag);
            writer.WriteEndTag();
            writer.WriteEndDocument();
        }

        using (var byteArrayInputStream = new MemoryStream(serverData))
        using (var zipInputStream = new ZipArchive(byteArrayInputStream))
        {
            foreach (var entry in zipInputStream.Entries)
            {
                if (entry.IsDirectory)
                {
                    continue;
                }

                var path = Path.Combine(worldDir.FullName, entry.FullName);

                using (var zipStream = await entry.OpenAsync())
                using (var fs = File.OpenWriteNew(path))
                {
                    await zipStream.CopyToAsync(fs);
                }
            }
        }

        return workDir;
    }

    private static bool CopyServerFile(FileSystemInfo src, FileSystemInfo dst, bool directory)
    {
        if (!src.Exists)
        {
            return false;
        }

        if (directory)
        {
            ((DirectoryInfo)src).CopyTo(dst.FullName);
        }
        else
        {
            ((FileInfo)src).CopyTo(dst.FullName);
        }

        return true;
    }

    private static TagCompound CreateLevelDat(bool survival, bool night)
    {
        TagCompound dataTag = new NbtBuilder.Compound()
            .Add("GameType", survival ? 0 : 1)
            .Add("Difficulty", 1)
            .Add("DayTime", !night ? 6000 : 18000)
            .Add("GameRules", new NbtBuilder.Compound()
                .Add("doDaylightCycle", "false")
                .Add("doWeatherCycle", "false")
                .Add("doMobSpawning", "false")
                .Add("fountain:doMobDespawn", "false")
                .Add("keepInventory", "true")
            )
            .Add("WorldGenSettings", new NbtBuilder.Compound()
                .Add("seed", (long)0)    // TODO
                .Add("generate_features", (byte)0)
                .Add("dimensions", new NbtBuilder.Compound()
                    .Add("minecraft:overworld", new NbtBuilder.Compound()
                        .Add("type", "minecraft:overworld")
                        .Add("generator", new NbtBuilder.Compound()
                            .Add("type", "fountain:wrapper")
                            .Add("buildplate", new NbtBuilder.Compound()
                                .Add("ground_level", 63))
                            .Add("inner", new NbtBuilder.Compound()
                                .Add("type", "minecraft:noise")
                                .Add("settings", "minecraft:overworld")
                                .Add("biome_source", new NbtBuilder.Compound()
                                    .Add("type", "minecraft:multi_noise")
                                    .Add("preset", "minecraft:overworld")
                                )
                            )
                        )
                    )
                    .Add("minecraft:the_nether", new NbtBuilder.Compound()
                        .Add("type", "minecraft:the_nether")
                        .Add("generator", new NbtBuilder.Compound()
                            .Add("type", "fountain:wrapper")
                            .Add("buildplate", new NbtBuilder.Compound()
                                .Add("ground_level", 32))
                            .Add("inner", new NbtBuilder.Compound()
                                .Add("type", "minecraft:noise")
                                .Add("settings", "minecraft:nether")
                                .Add("biome_source", new NbtBuilder.Compound()
                                    .Add("type", "minecraft:fixed")
                                    .Add("biome", "minecraft:nether_wastes")
                                )
                            )
                        )
                    )
                )
            )
            .Add("DataVersion", 3700)
            .Add("version", 19133)
            .Add("Version", new NbtBuilder.Compound()
                .Add("Id", 3700)
                .Add("Name", "1.20.4")
                .Add("Series", "main")
                .Add("Snapshot", (byte)0)
            )
            .Add("initialized", (byte)1)
            .Build("Data");

        return dataTag;
    }

#pragma warning disable IDE0060 // Remove unused parameter
    private DirectoryInfo? SetupBridgeFiles(byte[] serverData)
#pragma warning restore IDE0060 // Remove unused parameter
    {
        var workDir = new DirectoryInfo(Path.Combine(_baseDir.FullName, "bridge"));
        try
        {
            workDir.Create();
        }
        catch (IOException exception)
        {
            LogBridgeDirectoryCreateFail(exception);
            return null;
        }

        // empty

        return workDir;
    }

    private void CleanupBaseDir()
    {
        LogCleaningUpRuntimeDirectory();

        try
        {
            _baseDir.Delete(recursive: true);
        }
        catch (Exception exception)
        {
            LogCleanUpRuntimeDirectoryError(exception);
        }
    }

    private async Task StartServerProcessAsync()
    {
        await using (await _subprocessLock.LockAsync(CancellationToken.None))
        {
            if (_shuttingDown)
            {
                LogSkippingServerStartAlreadyShuttingDown();
                return;
            }

            if (_serverProcess is not null)
            {
                LogSkippingServerStartAlreadyStarted();
                return;
            }

            LogStartingServerProcess();

            try
            {
                var useShellExecute = true;
                var redirect = false;

                _serverProcess = new ConsoleProcess(_javaCmd, _logger, useShellExecute: useShellExecute, redirect: redirect, openInNewWindow: true);

                var serverLogger = _loggerFactory.CreateLogger($"{nameof(Instance)}({Port}/{_serverInternalPort}/server)");

                if (redirect && !useShellExecute)
                {
                    _serverProcess.StandartTextReceived += (sender, e) =>
                    {
                        if (!string.IsNullOrWhiteSpace(e.Data))
                        {
                            LogReceivedServerData(serverLogger, LogLevel.Information, e.Data);
                        }
                    };
                    _serverProcess.ErrorTextReceived += (sender, e) =>
                    {
                        if (!string.IsNullOrWhiteSpace(e.Data))
                        {
                            LogReceivedServerData(serverLogger, LogLevel.Error, e.Data);
                        }
                    };
                }

                await _serverProcess.ExecuteAsync(_serverWorkDir.FullName, ["-jar", _fabricJarName, "-nogui"]);

                LogServerProcessStarted(_serverProcess.Id);
            }
            catch (IOException exception)
            {
                LogServerProcessStartError(exception);
            }
        }
    }

    private async Task StartBridgeProcessAsync()
    {
        await using (await _subprocessLock.LockAsync(CancellationToken.None))
        {
            if (_shuttingDown)
            {
                LogSkippingBridgeStartAlreadyShuttingDown();
                return;
            }

            if (_bridgeProcess is not null)
            {
                LogSkippingBridgeStartAlreadyStarted();
                return;
            }

            LogStartingBridgeProcess();

            try
            {
                var useShellExecute = true;
                var redirect = false;

                var bridgeLogger = _loggerFactory.CreateLogger($"{nameof(Instance)}({Port}/{_serverInternalPort}/bridge)");

                _bridgeProcess = new ConsoleProcess(_javaCmd, _logger, useShellExecute: useShellExecute, redirect: redirect, openInNewWindow: true);
                if (redirect && !useShellExecute)
                {
                    _bridgeProcess.StandartTextReceived += (sender, e) =>
                    {
                        if (!string.IsNullOrWhiteSpace(e.Data))
                        {
                            LogReceivedBridgeData(bridgeLogger, LogLevel.Information, e.Data);
                        }
                    };
                    _bridgeProcess.ErrorTextReceived += (sender, e) =>
                    {
                        if (!string.IsNullOrWhiteSpace(e.Data))
                        {
                            LogReceivedBridgeData(bridgeLogger, LogLevel.Error, e.Data);
                        }
                    };
                }

                _bridgeProcess.ProcessExited += (sender, e) =>
                {
                    Task.Run(async () =>
                    {
                        await using (await _subprocessLock.LockAsync(CancellationToken.None))
                        {
                            if (!_shuttingDown)
                            {
                                LogBridgeProcessUnexpectedTermination(_bridgeProcess.ExitCode);
                                _bridgeProcess.Dispose();
                                _bridgeProcess = null;
                                BeginShutdown();
                            }
                        }
                    }).Forget();
                };

                await _bridgeProcess.ExecuteAsync(_bridgeWorkDir!.FullName,
                [
                    "-jar", _fountainBridgeJar.FullName,
                    "-port", Port.ToString(CultureInfo.InvariantCulture),
                    "-serverAddress", "127.0.0.1",
                    "-serverPort", _serverInternalPort.ToString(CultureInfo.InvariantCulture),
                    "-connectorPluginJar", _connectorPluginJar.FullName,
                    "-connectorPluginClass", "micheal65536.vienna.buildplate.connector.plugin.ViennaConnectorPlugin",
                    "-connectorPluginArg", _connectorPluginArgString,
                    "-useUUIDAsUsername",
                ]);

                LogBridgeProcessStarted(_bridgeProcess.Id);
            }
            catch (IOException exception)
            {
                LogBridgeProcessStartError(exception);
            }
        }
    }

    private void StartHostPlayerConnectTimeout()
        => Task.Run(async () =>
        {
            await Task.Delay(checked((int)HOST_PLAYER_CONNECT_TIMEOUT));

            await using (await _subprocessLock.LockAsync(CancellationToken.None))
            {
                if (_shuttingDown)
                {
                    return;
                }
            }

            if (!_hostPlayerConnected)
            {
                LogHostConnectTimerDone();
                BeginShutdown();
            }
        }).Forget();

    private void StartShutdownTimer()
        => Task.Run(async () =>
        {
            await Task.Yield();

            if (_shutdownTime is { } shutdownTime)
            {
                var currentTime = DateTimeOffset.UtcNow;
                while (currentTime < shutdownTime)
                {
                    var duration = shutdownTime - currentTime;
                    if (duration > TimeSpan.Zero)
                    {
                        LogShutdownTimerProgress((long)duration.TotalSeconds);
                        await Task.Delay(checked((int)(duration.TotalMilliseconds > 2000 ? (duration.TotalMilliseconds / 2) : duration.TotalMilliseconds)));
                    }

                    currentTime = DateTimeOffset.UtcNow;
                }
            }

            LogShutdownTimerDone();
            BeginShutdown();
        }).Forget();

    private void BeginShutdown()
        => Task.Run(async () =>
        {
            await Task.Yield();

            var @lock = await _subprocessLock.LockAsync(CancellationToken.None);

            if (_shuttingDown)
            {
                LogShutDownAlreadyInProgress();
                await @lock.DisposeAsync();
                return;
            }

            _shuttingDown = true;

            LogBeginningShutdown();

            SendEventBusInstanceStatusNotification("shuttingDown");

            if (_bridgeProcess is not null)
            {
                LogWaitingForBridgeToShutDown();
                await @lock.DisposeAsync();
                await _bridgeProcess.StopAndWaitAsync(_logger);
                var exitCode = _bridgeProcess.ExitCodeText;
                @lock = await _subprocessLock.LockAsync(CancellationToken.None);
                _bridgeProcess.Dispose();
                _bridgeProcess = null;
                LogBridgeProcessFinished(exitCode);
            }

            if (_serverProcess is not null)
            {
                LogAskingTheServerToShutDown();
                await _serverProcess.StopNoWaitAsync(_logger);
            }

            await @lock.DisposeAsync();
        }).Forget();

    public async Task WaitForShutdownAsync()
    {
        while (_thread is null)
        {
            await Task.Delay(50);
        }

        await _thread;
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

    [JsonConverter(typeof(JsonStringEnumConverter<BuildplateSource>))]
    internal enum BuildplateSource
    {
        PLAYER,
        SHARED,
        ENCOUNTER,
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Starting for player {PlayerId} buildplate {BuildplateId} (survival = {SurvivalEnabled}, saveEnabled = {SaveEnabled}, inventoryType = {InventoryType})")]
    private partial void LogStartingForPlayer(Guid? PlayerId, Guid BuildplateId, bool SurvivalEnabled, bool SaveEnabled, InventoryType InventoryType);

    [LoggerMessage(Level = LogLevel.Information, Message = "Starting for shared buildplate {BuildplateId} (player = {PlayerId}, survival = {SurvivalEnabled}, saveEnabled = {SaveEnabled}, inventoryType = {InventoryType})")]
    private partial void LogStartingForSharedBuildplate(Guid BuildplateId, Guid? PlayerId, bool SurvivalEnabled, bool SaveEnabled, InventoryType InventoryType);

    [LoggerMessage(Level = LogLevel.Information, Message = "Starting for encounter buildplate {BuildplateId} (player = {PlayerId}, survival = {SurvivalEnabled}, saveEnabled = {SaveEnabled}, inventoryType = {InventoryType})")]
    private partial void LogStartingForEncounterBuildplate(Guid BuildplateId, Guid? PlayerId, bool SurvivalEnabled, bool SaveEnabled, InventoryType InventoryType);

    [LoggerMessage(Level = LogLevel.Information, Message = "Using port {Port} internal port {ServerInternalPort}")]
    private partial void LogPortUsageInfo(int Port, int ServerInternalPort);

    [LoggerMessage(Level = LogLevel.Information, Message = "Setting up server")]
    private partial void LogSettingUpServer();

    [LoggerMessage(Level = LogLevel.Error, Message = "Buildplate load response contained invalid base64 data")]
    private partial void LogBuildplateLoadInvalidBase64(Exception exception);

    [LoggerMessage(Level = LogLevel.Error, Message = "Could not set up files for server")]
    private partial void LogSetupServerFilesError();

    [LoggerMessage(Level = LogLevel.Error, Message = "Could not set up files for server")]
    private partial void LogSetupServerFilesError(Exception exception);

    [LoggerMessage(Level = LogLevel.Error, Message = "Could not set up files for bridge")]
    private partial void LogSetupBridgeFilesError();

    [LoggerMessage(Level = LogLevel.Error, Message = "Could not set up files for bridge")]
    private partial void LogSetupBridgeFilesError(Exception exception);

    [LoggerMessage(Level = LogLevel.Information, Message = "Running server")]
    private partial void LogRunningServer();

    [LoggerMessage(Level = LogLevel.Error, Message = "Event bus subscriber error")]
    private partial void LogEventBusSubscriberError(Exception? exception);

    [LoggerMessage(Level = LogLevel.Error, Message = "Event bus request handler error")]
    private partial void LogEventBusRequestHandlerError(Exception? exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Server process has unexpectedly terminated with exit code {ExitCode}")]
    private partial void LogServerProcessUnexpectedExit(string ExitCode);

    [LoggerMessage(Level = LogLevel.Information, Message = "Server has finished with exit code {ExitCode}")]
    private partial void LogServerProcessFinished(string ExitCode);

    [LoggerMessage(Level = LogLevel.Information, Message = "Bridge is still running, shutting it down now")]
    private partial void LogWaitingForBridge();

    [LoggerMessage(Level = LogLevel.Information, Message = "Bridge has finished with exit code {ExitCode}")]
    private partial void LogBridgeProcessFinished(string ExitCode);

    [LoggerMessage(Level = LogLevel.Error, Message = "Server failed to start")]
    private partial void LogServerStartFailed();

    [LoggerMessage(Level = LogLevel.Error, Message = "Unhandled exception")]
    private partial void LogUnhandledException(Exception exception);

    [LoggerMessage(Level = LogLevel.Information, Message = "Finished")]
    private partial void LogInstanceFinished();

    [LoggerMessage(Level = LogLevel.Information, Message = "Server is ready")]
    private partial void LogServerIsReady();

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to decode event bus message JSON")]
    private partial void LogEventBusDecodeError(Exception exception);

    [LoggerMessage(Level = LogLevel.Error, Message = "Event bus publisher error")]
    private partial void LogEventBusPublisherError();

    [LoggerMessage(Level = LogLevel.Information, Message = "Saving snapshot")]
    private partial void LogSavingSnapshot();

    [LoggerMessage(Level = LogLevel.Information, Message = "Not saving snapshot because host player never connected")]
    private partial void LogNotSavingSnapshotHostNotConnected();

    [LoggerMessage(Level = LogLevel.Information, Message = "Rejecting player connection for player {PlayerUuid} because the host player must connect first")]
    private partial void LogRejectingPlayerConnectionHostNotFirst(Guid PlayerUuid);

    [LoggerMessage(Level = LogLevel.Information, Message = "Player {PlayerUuid} has connected")]
    private partial void LogPlayerConnected(Guid PlayerUuid);

    [LoggerMessage(Level = LogLevel.Information, Message = "Player {PlayerUuid} has disconnected")]
    private partial void LogPlayerDisconnected(Guid PlayerUuid);

    [LoggerMessage(Level = LogLevel.Information, Message = "Host player has disconnected, beginning shutdown")]
    private partial void LogHostPlayerDisconnected();

    [LoggerMessage(Level = LogLevel.Error, Message = "Event bus request failed (no response)")]
    private partial void LogEventBusRequestNoResponse();

    [LoggerMessage(Level = LogLevel.Error, Message = "Event bus request failed")]
    private partial void LogEventBusRequestFail(Exception exception);

    [LoggerMessage(Level = LogLevel.Error, Message = "Could not create server working directory")]
    private partial void LogServerDirectoryCreateFail(Exception exception);

    [LoggerMessage(Level = LogLevel.Error, Message = "Fabric JAR '{FabricJarPath}' does not exist in server template directory")]
    private partial void LogFabricJarNotFound(string FabricJarPath);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Server files were not pre-downloaded in server template directory, it is recommended to pre-download all server files to improve instance start-up time and reduce network data usage")]
    private partial void LogServerFilesNotPreDownloaded();

    [LoggerMessage(Level = LogLevel.Error, Message = "Mods directory was not present in server template directory, the buildplate server instance will not function correctly without the Fountain and Vienna Fabric mods installed")]
    private partial void LogModsDirrectoryNotFound();

    [LoggerMessage(Level = LogLevel.Error, Message = "Could not create server world directory")]
    private partial void LogServerWorldDirectoryCreateFail(Exception exception);

    [LoggerMessage(Level = LogLevel.Error, Message = "Could not create server world entities directory")]
    private partial void LogServerWorldEntitiesDirectoryCreateFail(Exception exception);

    [LoggerMessage(Level = LogLevel.Error, Message = "Could not create server world regions directory")]
    private partial void LogServerWorldRegionsDirectoryCreateFail(Exception exception);

    [LoggerMessage(Level = LogLevel.Error, Message = "Could not create bridge working directory")]
    private partial void LogBridgeDirectoryCreateFail(Exception exception);

    [LoggerMessage(Level = LogLevel.Information, Message = "Cleaning up runtime directory")]
    private partial void LogCleaningUpRuntimeDirectory();

    [LoggerMessage(Level = LogLevel.Error, Message = "Exception while cleaning up runtime directory")]
    private partial void LogCleanUpRuntimeDirectoryError(Exception exception);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Already shutting down, not starting server process")]
    private partial void LogSkippingServerStartAlreadyShuttingDown();

    [LoggerMessage(Level = LogLevel.Debug, Message = "Server process has already been started")]
    private partial void LogSkippingServerStartAlreadyStarted();

    [LoggerMessage(Level = LogLevel.Information, Message = "Starting server process")]
    private partial void LogStartingServerProcess();

    [LoggerMessage(Message = "[server] {Message}")]
    private static partial void LogReceivedServerData(ILogger logger, LogLevel logLevel, string Message);

    [LoggerMessage(Level = LogLevel.Information, Message = "Server process started, PID: {Pid}")]
    private partial void LogServerProcessStarted(int Pid);

    [LoggerMessage(Level = LogLevel.Error, Message = "Could not start server process")]
    private partial void LogServerProcessStartError(Exception exception);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Already shutting down, not starting bridge process")]
    private partial void LogSkippingBridgeStartAlreadyShuttingDown();

    [LoggerMessage(Level = LogLevel.Debug, Message = "Bridge process has already been started")]
    private partial void LogSkippingBridgeStartAlreadyStarted();

    [LoggerMessage(Level = LogLevel.Information, Message = "Starting bridge process")]
    private partial void LogStartingBridgeProcess();

    [LoggerMessage(Message = "[bridge] {Message}")]
    private static partial void LogReceivedBridgeData(ILogger logger, LogLevel logLevel, string Message);

    [LoggerMessage(Level = LogLevel.Information, Message = "Bridge process started, PID: {Pid}")]
    private partial void LogBridgeProcessStarted(int Pid);

    [LoggerMessage(Level = LogLevel.Error, Message = "Could not start bridge process")]
    private partial void LogBridgeProcessStartError(Exception exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Bridge process has unexpectedly terminated with exit code {ExitCode}")]
    private partial void LogBridgeProcessUnexpectedTermination(int? ExitCode);

    [LoggerMessage(Level = LogLevel.Information, Message = "Host player has not connected yet, shutting down")]
    private partial void LogHostConnectTimerDone();

    [LoggerMessage(Level = LogLevel.Information, Message = "Server will shut down in {DurationMiliseconds} seconds")]
    private partial void LogShutdownTimerProgress(long DurationMiliseconds);

    [LoggerMessage(Level = LogLevel.Information, Message = "Shutdown time has been reached, shutting down")]
    private partial void LogShutdownTimerDone();

    [LoggerMessage(Level = LogLevel.Debug, Message = "Already shutting down, not beginning shutdown1")]
    private partial void LogShutDownAlreadyInProgress();

    [LoggerMessage(Level = LogLevel.Information, Message = "Beginning shutdown")]
    private partial void LogBeginningShutdown();

    [LoggerMessage(Level = LogLevel.Information, Message = "Waiting for bridge to shut down")]
    private partial void LogWaitingForBridgeToShutDown();

    [LoggerMessage(Level = LogLevel.Information, Message = "Asking the server to shut down")]
    private partial void LogAskingTheServerToShutDown();
}