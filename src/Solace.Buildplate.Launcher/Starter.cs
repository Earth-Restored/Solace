using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Logging;
using Solace.Buildplate.Common;
using Solace.Buildplate.Connector.Model;
using Solace.Common.Utils;
using Solace.EventBus.Client;

namespace Solace.Buildplate.Launcher;

internal sealed partial class Starter
{
    private readonly EventBusClient _eventBusClient;

    private readonly string _publicAddress;
    private readonly ushort _basePort;
    private readonly string _javaCmd;
    private readonly DirectoryInfo _tmpDir;
    private readonly string _eventBusConnectionString;

    private readonly FileInfo _fountainBridgeJar;
    private readonly DirectoryInfo _serverTemplateDir;
    private readonly string _fabricJarName;
    private readonly FileInfo _connectorPluginJar;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<Starter> _logger;

    private const ushort SERVER_INTERNAL_BASE_PORT = 25565;
    private readonly HashSet<int> _portsInUse = [];
    private readonly HashSet<int> _serverInternalPortsInUse = [];

    public Starter(EventBusClient eventBusClient, string eventBusConnectionString, string publicAddress, ushort basePort, string javaCmd, string bridgeJar, string serverTemplateDir, string fabricJarName, string connectorPluginJar, ILoggerFactory loggerFactory, ILogger<Starter> logger)
    {
        _eventBusClient = eventBusClient;

        _publicAddress = publicAddress;
        _basePort = basePort;
        _javaCmd = javaCmd;
        _tmpDir = new DirectoryInfo(Path.GetTempPath());
        _eventBusConnectionString = eventBusConnectionString;

        _fountainBridgeJar = new FileInfo(Path.GetFullPath(bridgeJar));
        _serverTemplateDir = new DirectoryInfo(Path.GetFullPath(serverTemplateDir));
        _fabricJarName = fabricJarName;
        _connectorPluginJar = new FileInfo(connectorPluginJar);
        _logger = logger;
        _loggerFactory = loggerFactory;
    }

    public async Task<Instance?> StartInstanceAsync(Guid instanceId, Guid? playerId, Guid buildplateId, Instance.BuildplateSource buildplateSource, bool survival, bool night, bool saveEnabled, InventoryType inventoryType, DateTimeOffset? shutdownTime, CancellationToken cancellationToken = default)
    {
        await ServerUtils.WaitForSetup(App.StaticDataPath, _logger, cancellationToken);

        var baseDir = CreateInstanceBaseDir(instanceId);
        if (baseDir is null)
        {
            return null;
        }

        var port = FindPort(_portsInUse, _basePort);
        var serverInternalPort = FindPort(_serverInternalPortsInUse, SERVER_INTERNAL_BASE_PORT);

        var instanceLogger = _loggerFactory.CreateLogger($"{nameof(Instance)}({port}/{serverInternalPort})");
        var instance = Instance.Run(_eventBusClient, playerId, buildplateId, buildplateSource, instanceId, survival, night, saveEnabled, inventoryType, shutdownTime, _publicAddress, port, serverInternalPort, _javaCmd, _fountainBridgeJar, _serverTemplateDir, _fabricJarName, _connectorPluginJar, baseDir, _eventBusConnectionString, _loggerFactory, instanceLogger);

        Task.Run(async () =>
        {
            await instance.WaitForShutdownAsync();
            ReleasePort(_portsInUse, port);
            ReleasePort(_serverInternalPortsInUse, serverInternalPort);
        }, CancellationToken.None).Forget();

        return instance;
    }

    private static int FindPort(HashSet<int> portsInUse, int basePort)
    {
        lock (portsInUse)
        {
            var port = basePort;
            while (portsInUse.Contains(port) || !CanBindPort(port))
            {
                port++;
            }

            portsInUse.Add(port);
            return port;
        }
    }

    private static bool CanBindPort(int port)
    {
        try
        {
            using var listener = new TcpListener(IPAddress.Any, port);
            listener.Start();
            using var udpClient = new UdpClient(port);
            return true;
        }
        catch (SocketException)
        {
            return false;
        }
    }

    private static void ReleasePort(HashSet<int> portsInUse, int port)
    {
        lock (portsInUse)
        {
            if (!portsInUse.Remove(port))
            {
                throw new UnreachableException();
            }
        }
    }

    private DirectoryInfo? CreateInstanceBaseDir(Guid instanceId)
    {
        var directory = new DirectoryInfo(Path.Combine(_tmpDir.FullName, $"vienna-buildplate-instance_{instanceId}"));
        try
        {
            directory.Create();
        }
        catch (IOException exception)
        {
            LogCreateBaseDirectoryFail(exception, instanceId);
        }

        LogCreateBaseDirectorySucceed(directory.FullName);
        return directory;
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Error creating instance base directory for '{InstanceId}'")]
    private partial void LogCreateBaseDirectoryFail(Exception Exception, Guid InstanceId);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Created instance base directory: {BaseDirectoryPath}")]
    private partial void LogCreateBaseDirectorySucceed(string BaseDirectoryPath);
}
