using Solace.EventBus.Client;

namespace Solace.AuthServer;

public sealed partial class PlayfabDataReloader : IHostedService, IAsyncDisposable
{
    private readonly EventBusClient _eventBus;
    private readonly Features.PlayfabApi.Catalog.CatalogService _catalog;
    private readonly ILogger<PlayfabDataReloader> _logger;

    private Subscriber? _subscriber;

    public PlayfabDataReloader(Features.PlayfabApi.Catalog.CatalogService catalog, EventBusClient eventBus, ILogger<PlayfabDataReloader> logger)
    {
        _catalog = catalog;
        _eventBus = eventBus;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (_subscriber is not null)
        {
            await _subscriber.DisposeAsync();
        }

        _subscriber = await _eventBus.AddSubscriberAsync(
            "playfab",
            HandlePlayfabEvent,
            async exception =>
            {
                LogPlayfabEventBusSubscriberError(exception);
                Console.Error.WriteLine(exception);
                Console.Error.Flush();
                Environment.Exit(1);
            });
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_subscriber is not null)
        {
            await _subscriber.DisposeAsync();
        }

        _subscriber = null;
    }

    public async ValueTask DisposeAsync()
    {
        if (_subscriber is not null)
        {
            await _subscriber.DisposeAsync();
        }

        _subscriber = null;
    }

    private async Task HandlePlayfabEvent(SubscriberEvent @event, CancellationToken cancellationToken)
    {
        switch (@event.Type)
        {
            case "shop_data_updated":
                {
                    _catalog.ClearCache();
                    LogCacheCleared();
                }

                break;
        }
    }

    [LoggerMessage(Level = LogLevel.Critical, Message = "Playfab event bus subscriber error")]
    private partial void LogPlayfabEventBusSubscriberError(Exception? exception);

    [LoggerMessage(Level = LogLevel.Information, Message = "Playfab data cache cleared, will load from db on next request")]
    private partial void LogCacheCleared();
}
