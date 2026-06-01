using System.Diagnostics;
using Serilog;
using Solace.Common.Utils;
using Solace.LauncherUI.Programs;
using Solace.LauncherUI.Utils;

namespace Solace.LauncherUI;

public sealed class ServerComponent
{
    public string Name { get; }
    public string ExeName { get; }
    public Func<Settings, Serilog.ILogger, Process?> StartAction { get; }
    public int StartupDelayMs { get; }
    public Func<Settings, bool> IsEnabled { get; }

    public ServerStatus Status { get; set; } = ServerStatus.Offline;

    public ServerComponent(string name, string exeName, Func<Settings, Serilog.ILogger, Process?> startAction, int startupDelayMs = 0, Func<Settings, bool>? isEnabled = null)
    {
        Name = name;
        ExeName = exeName;
        StartAction = startAction;
        StartupDelayMs = startupDelayMs;
        IsEnabled = isEnabled ?? (_ => true);
    }
}

public sealed class ServerManager : IDisposable
{
    public event Action? OnStatusChanged;

    private ServerStatus _status = ServerStatus.Offline;
    public ServerStatus Status
    {
        get => _status;
        private set
        {
            if (_status != value)
            {
                _status = value;
                OnStatusChanged?.Invoke();
            }
        }
    }

    public bool AnyOnline { get; private set; }

    private int _startLockCount;
    public bool StartLocked => Volatile.Read(ref _startLockCount) > 0;

    public bool CanStart => !StartLocked && Status is not (ServerStatus.Starting or ServerStatus.Online);
    public bool CanRestart => Status is not (ServerStatus.Stopping or ServerStatus.Offline);
    public bool CanStop => Status is not (ServerStatus.Stopping or ServerStatus.Offline);

    public IReadOnlyList<ServerComponent> Components { get; }

    private readonly Lock _statusLock = new Lock();

    private CancellationTokenSource? _operationTokenSource;

    public ServerManager()
    {
        Components =
        [
            new("Event Bus", EventBusServer.ExeName, EventBusServer.Run),
            new("Object Store", ObjectStoreServer.ExeName, ObjectStoreServer.Run, 1000),
            new("Buildplate Launcher", BuildplateLauncher.ExeName, BuildplateLauncher.Run, 1500),
            new("API Server", ApiServer.ExeName, ApiServer.Run),
            new("Tappables Generator", TappablesGenerator.ExeName, TappablesGenerator.Run),
            new("Tile Renderer", TileRenderer.ExeName, TileRenderer.Run, 0, s => s.EnableTileRenderingLabel ?? true)
        ];

        RefreshComponentStatuses();
    }

    public void RefreshComponentStatuses(bool detectRunning = true, bool preserveCurrentStatus = false)
    {
        var settings = Settings.Instance;
        bool anyOnline = false;

        foreach (var comp in Components)
        {
            if (detectRunning)
            {
                bool isRunning = ProcessUtils.GetProgramProcesses(comp.ExeName).Any();
                comp.Status = comp.Status switch
                {
                    ServerStatus.Starting => isRunning ? ServerStatus.Online : ServerStatus.Starting,
                    ServerStatus.Stopping => isRunning ? ServerStatus.Stopping : ServerStatus.Offline,
                    _ => isRunning ? ServerStatus.Online : ServerStatus.Offline,
                };
            }

            if (comp.Status is ServerStatus.Online && comp.IsEnabled(settings))
            {
                anyOnline = true;
            }
        }

        AnyOnline = anyOnline;
        var newStatus = ComputeGlobalStatus();
        if (preserveCurrentStatus && Status is ServerStatus.Starting && newStatus is ServerStatus.Offline)
        {
            return;
        }

        if (preserveCurrentStatus && Status is ServerStatus.Stopping && newStatus is ServerStatus.Offline)
        {
            return;
        }

        Status = newStatus;
    }

    public IDisposable? AcquireStartLock()
    {
        lock (_statusLock)
        {
            if (Status is ServerStatus.Offline)
            {
                return null;
            }

            return new StartLockHandle(this);
        }
    }

    private ServerStatus ComputeGlobalStatus()
    {
        if (Components.Any(c => c.Status is ServerStatus.Stopping))
        {
            return ServerStatus.Stopping;
        }

        if (Components.Any(c => c.Status is ServerStatus.Starting))
        {
            return ServerStatus.Starting;
        }

        var enabledComponents = Components.Where(c => c.IsEnabled(Settings.Instance)).ToList();
        var onlineCount = enabledComponents.Count(c => c.Status is ServerStatus.Online);
        var offlineCount = enabledComponents.Count(c => c.Status is ServerStatus.Offline);

        if (enabledComponents.Count == 0)
        {
            return ServerStatus.Offline;
        }

        if (onlineCount > 0 && offlineCount > 0)
        {
            return ServerStatus.PartiallyOnline;
        }

        return onlineCount > 0 ? ServerStatus.Online : ServerStatus.Offline;
    }

    private static async Task<bool> WaitForProcessStartAsync(string exeName, CancellationToken cancellationToken)
    {
        const int intervalMs = 200;
        const int maxAttempts = 50;

        for (var attempt = 0; attempt < maxAttempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (ProcessUtils.GetProgramProcesses(exeName).Any())
            {
                return true;
            }

            await Task.Delay(intervalMs, cancellationToken);
        }

        return false;
    }

    public async Task<bool> EnsureComponentsOnline(params string[] exeNames)
    {
        if (exeNames is null || exeNames.Length == 0)
        {
            return true;
        }

        List<ServerComponent> targets;
        var reservedToStart = new HashSet<ServerComponent>();

        lock (_statusLock)
        {
            if (StartLocked)
            {
                Log.Logger.Warning("EnsureComponentsOnline blocked because server start is locked.");
                return false;
            }

            // Identify which of our managed components match the requested EXEs
            targets = [.. Components.Where(c => exeNames.Contains(c.ExeName, StringComparer.OrdinalIgnoreCase))];

            if (targets.Count == 0)
            {
                return true;
            }

            if (Status is ServerStatus.Stopping)
            {
                return false;
            }

            foreach (var comp in targets)
            {
                if (comp.Status is ServerStatus.Offline)
                {
                    comp.Status = ServerStatus.Starting;
                    reservedToStart.Add(comp);
                }
            }

            if (targets.All(t => t.Status is ServerStatus.Online))
            {
                return true;
            }

            if (Status is ServerStatus.Offline or ServerStatus.PartiallyOnline)
            {
                Status = ServerStatus.Starting;
            }
        }

        var logger = Log.Logger;
        var settings = Settings.Instance;
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30)); // Safety timeout

        try
        {
            foreach (var comp in targets)
            {
                if (comp.Status is ServerStatus.Online)
                {
                    continue;
                }

                bool shouldStart = reservedToStart.Contains(comp);
                if (shouldStart)
                {
                    logger.Debug($"Page-level initialization: Starting {comp.Name}");
                }
                else
                {
                    logger.Debug($"Page-level initialization: Waiting for {comp.Name} to become online");
                }

                var process = shouldStart ? comp.StartAction(settings, logger) : null;
                if (comp.StartupDelayMs > 0)
                {
                    await Task.Delay(comp.StartupDelayMs, cts.Token);
                }

                if (await WaitForProcessStartAsync(comp.ExeName, cts.Token))
                {
                    comp.Status = ServerStatus.Online;
                }
                else
                {
                    comp.Status = ServerStatus.Offline;
                    if (process is not null && process.HasExited)
                    {
                        logger.Error($"{comp.Name} process exited immediately during selective startup.");
                    }
                    else
                    {
                        logger.Error($"{comp.Name} failed to start during page-level initialization.");
                    }
                }

                OnStatusChanged?.Invoke();
            }
        }
        catch (OperationCanceledException)
        {
            logger.Warning("Selective component startup cancelled.");
            return false;
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error during selective component startup");
            return false;
        }
        finally
        {
            RefreshComponentStatuses();
        }

        return targets.All(t => t.Status is ServerStatus.Online);
    }

    public async Task Start(CancellationToken cancellationToken = default)
    {
        lock (_statusLock)
        {
            if (!CanStart)
            {
                return;
            }

            cancellationToken = InitOperation(cancellationToken);
            Status = ServerStatus.Starting;
        }

        try
        {
            await StartInternal(Log.Logger, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            await Stop(default);
        }
    }

    public async Task Stop(CancellationToken cancellationToken = default)
    {
        lock (_statusLock)
        {
            if ((Status is ServerStatus.Offline && !AnyOnline) || Status is ServerStatus.Stopping)
            {
                return;
            }

            cancellationToken = InitOperation(cancellationToken);
            Status = ServerStatus.Stopping;
        }

        foreach (var comp in Components)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (comp.Status is ServerStatus.Offline)
            {
                continue;
            }

            comp.Status = ServerStatus.Stopping;
            OnStatusChanged?.Invoke();

            await StopProgram(comp.ExeName, Log.Logger, cancellationToken);

            comp.Status = ServerStatus.Offline;
            OnStatusChanged?.Invoke();
        }

        cancellationToken.ThrowIfCancellationRequested();
        AnyOnline = false;
        Status = ServerStatus.Offline;
    }

    public async Task Restart(CancellationToken cancellationToken = default)
    {
        lock (_statusLock)
        {
            if (Status is ServerStatus.Stopping or ServerStatus.Offline)
            {
                return;
            }
        }

        await Stop(cancellationToken);

        lock (_statusLock)
        {
            if (StartLocked)
            {
                Log.Logger.Warning("Restart aborted because server start is locked after stop.");
                return;
            }
        }

        await Start(cancellationToken);
    }

    public async Task KillAll(CancellationToken cancellationToken = default)
    {
        lock (_statusLock)
        {
            cancellationToken = InitOperation(cancellationToken);
            Status = ServerStatus.Stopping;
        }

        foreach (var comp in Components)
        {
            cancellationToken.ThrowIfCancellationRequested();

            comp.Status = ServerStatus.Stopping;
            OnStatusChanged?.Invoke();

            await StopProgram(comp.ExeName, Log.Logger, cancellationToken);

            comp.Status = ServerStatus.Offline;
            OnStatusChanged?.Invoke();
        }

        cancellationToken.ThrowIfCancellationRequested();
        AnyOnline = false;
        Status = ServerStatus.Offline;
    }

    public void Dispose()
        => _operationTokenSource?.Dispose();

    private async Task StartInternal(Serilog.ILogger logger, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var settings = Settings.Instance;

        lock (_statusLock)
        {
            if (StartLocked)
            {
                logger.Warning("Start prevented because server start is locked.");
                Status = ServerStatus.Offline;
                return;
            }
        }

        if (!await FileChecker.CheckAsync(settings, false, logger, cancellationToken))
        {
            logger.Error("File validation failed");
            Status = ServerStatus.Offline;
            return;
        }

        RefreshComponentStatuses(preserveCurrentStatus: true);

        foreach (var comp in Components)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!comp.IsEnabled(settings))
            {
                comp.Status = ServerStatus.Offline;
                continue;
            }

            if (comp.Status is ServerStatus.Online)
            {
                logger.Debug($"{comp.Name} is already running.");
                continue;
            }

            comp.Status = ServerStatus.Starting;
            OnStatusChanged?.Invoke();

            var process = comp.StartAction(settings, logger);
            if (comp.StartupDelayMs > 0)
            {
                await Task.Delay(comp.StartupDelayMs, cancellationToken);
            }

            if (await WaitForProcessStartAsync(comp.ExeName, cancellationToken))
            {
                comp.Status = ServerStatus.Online;
                AnyOnline = true;
            }
            else
            {
                comp.Status = ServerStatus.Offline;
                if (process is not null && process.HasExited)
                {
                    logger.Error($"{comp.Name} process exited immediately after launch.");
                }
                else
                {
                    logger.Error($"{comp.Name} failed to start.");
                }
            }

            OnStatusChanged?.Invoke();
        }

        logger.Information("Waiting for programs to stabilize");
        await Task.Delay(7500, cancellationToken);

        bool error = false;
        foreach (var comp in Components)
        {
            if (!comp.IsEnabled(settings))
            {
                continue;
            }

            if (!ProcessUtils.GetProgramProcesses(comp.ExeName).Any())
            {
                logger.Error($"It was detected that {comp.Name} crashed/exited, make sure all options are set correctly, look into logs/{comp.Name}/logxxx for more info");
                comp.Status = ServerStatus.Offline;
                error = true;
            }
            else
            {
                comp.Status = ServerStatus.Online;
            }
        }

        RefreshComponentStatuses();

        if (!error)
        {
            logger.Information("All required programs have (most likely) running successfully");
        }
    }

    private static async Task StopProgram(string name, Serilog.ILogger logger, CancellationToken cancellationToken)
    {
        logger.Information($"Stopping {name}");

        int stoppedCount = 0;
        foreach (var process in ProcessUtils.GetProgramProcesses(name))
        {
            await process.StopGracefullyOrKillAndWaitAsync(3000, cancellationToken);
            stoppedCount++;
        }

        logger.Information(stoppedCount switch
        {
            0 => $"No {name} processes found",
            1 => $"Stopped 1 {name} process",
            _ => $"Stopped {stoppedCount} {name} processes",
        });
    }

    private CancellationToken InitOperation(CancellationToken cancellationToken)
    {
        _operationTokenSource?.Cancel();
        _operationTokenSource = null;

        _operationTokenSource = new CancellationTokenSource();
        var combinedSource = CancellationTokenSource.CreateLinkedTokenSource(_operationTokenSource.Token, cancellationToken);
        return combinedSource.Token;
    }

    private sealed class StartLockHandle : IDisposable
    {
        private readonly ServerManager _manager;
        private int _disposed;

        public StartLockHandle(ServerManager manager)
        {
            _manager = manager;
            if (Interlocked.Increment(ref _manager._startLockCount) == 1)
            {
                _manager.OnStatusChanged?.Invoke();
            }
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
            {
                return;
            }

            var remaining = Interlocked.Decrement(ref _manager._startLockCount);
            if (remaining <= 0)
            {
                Interlocked.Exchange(ref _manager._startLockCount, 0);
                _manager.OnStatusChanged?.Invoke();
            }
        }
    }
}

public enum ServerStatus
{
    Online = 0,
    Starting,
    Stopping,
    PartiallyOnline,
    Offline,
}