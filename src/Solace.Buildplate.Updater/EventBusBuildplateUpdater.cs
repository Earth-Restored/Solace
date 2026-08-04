using System.Buffers.Text;
using System.Runtime.Versioning;
using Microsoft.Extensions.Logging;
using Solace.EventBus.Client;

namespace Solace.Buildplate.Updater;

internal sealed partial class EventBusBuildplateUpdater : IAsyncDisposable
{
    private readonly EventBusClient _eventBus;

    private readonly BuildplateUpdater _updater;

    private readonly ILogger<EventBusBuildplateUpdater> _logger;

    public EventBusBuildplateUpdater(EventBusClient eventBus, BuildplateUpdater updater, ILogger<EventBusBuildplateUpdater> logger)
    {
        _eventBus = eventBus;
        _updater = updater;
        _logger = logger;
    }

    [SupportedOSPlatform("android")]
    [SupportedOSPlatform("linux")]
    [SupportedOSPlatform("windows")]
    public async Task RunAsync()
    {
        await _eventBus.AddRequestHandlerAsync("buildplate-update",
        async (request, cancellationToken) =>
        {
            if (request.Type is "updateBuildplate")
            {
                LogUpdating();

                var worldZip = Base64.DecodeFromChars(request.Data);

                byte[]? updateWorldZip;
                using (var worldZipStream = new MemoryStream(worldZip))
                {
                    try
                    {
                        updateWorldZip = await _updater.UpdateAsync(worldZipStream, cancellationToken);
                    }
                    catch (Exception exception)
                    {
                        LogUpdateError(exception);
                        throw;
                    }
                }

                if (updateWorldZip is null)
                {
                    return null;
                }

                LogUpdateDone();

                return Base64.EncodeToString(updateWorldZip);
            }
            else
            {
                return null;
            }
        }, async exception =>
        {
            LogEventBusSubscriberError(exception);
            Console.Error.WriteLine(exception);
            Console.Error.Flush();
            await DisposeAsync();
            Environment.Exit(1);
        });

        LogStarted();

        await Task.Delay(Timeout.Infinite);
    }

    public async ValueTask DisposeAsync()
        => await _eventBus.DisposeAsync();

    [LoggerMessage(Level = LogLevel.Critical, Message = "Event bus subscriber error")]
    private partial void LogEventBusSubscriberError(Exception? exception);

    [LoggerMessage(Level = LogLevel.Information, Message = "Updating buildplate")]
    private partial void LogUpdating();

    [LoggerMessage(Level = LogLevel.Error, Message = "An error occuted while updating the buildplate")]
    private partial void LogUpdateError(Exception exception);

    [LoggerMessage(Level = LogLevel.Information, Message = "Buildplate updated")]
    private partial void LogUpdateDone();

    [LoggerMessage(Level = LogLevel.Information, Message = "Started")]
    private partial void LogStarted();
}
