using System.Text.Json;
using Microsoft.Extensions.Logging;
using SkiaSharp;
using Solace.Common;
using Solace.EventBus.Client;

namespace Solace.TileRenderer;

internal sealed partial class EventBusTileRenderer : IAsyncDisposable
{
    private readonly ITileDataSource _dataSource;
    private readonly EventBusClient _eventBus;
    private readonly TileRenderer _renderer;

    private readonly ILogger<EventBusTileRenderer> _logger;

    public EventBusTileRenderer(ITileDataSource dataSource, EventBusClient eventBus, StaticData.StaticData staticData, ILogger<EventBusTileRenderer> logger)
    {
        _dataSource = dataSource;
        _eventBus = eventBus;
        _renderer = TileRenderer.Create(dataSource.GetTagMapJson(staticData.TileRenderer), logger);
        _logger = logger;
    }

    public async Task RunAsync()
    {
        await _eventBus.AddRequestHandlerAsync("tile",
        async request =>
        {
            if (request.Type is "renderTile")
            {
                RenderTileRequest getTile;
                try
                {
                    getTile = JsonSerializer.Deserialize(request.Data, AppJsonContext.Default.RenderTileRequest)!;
                }
                catch (Exception exception)
                {
                    LogCouldNotDeserialiseRenderTileRequest(exception);
                    return null;
                }

                LogRenderingTile(getTile.TileX, getTile.TileX, getTile.Zoom);

                using (var bitmap = new SKBitmap(128, 128))
                using (var canvas = new SKCanvas(bitmap))
                {
                    await _renderer.RenderAsync(_dataSource, canvas, getTile.TileX, getTile.TileY, getTile.Zoom, _logger);

                    // TODO: higher/lower quality?
                    using (var data = bitmap.Encode(SKEncodedImageFormat.Png, 80))
                    using (var stream = new MemoryStream())
                    {
                        data.SaveTo(stream);

                        LogSendingRenderedTile();
                        return Convert.ToBase64String(stream.ToArray());
                    }
                }
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

        while (true)
        {
            await Task.Delay(1000);
        }
    }

    public async ValueTask DisposeAsync()
        => await _eventBus.DisposeAsync();

    [LoggerMessage(Level = LogLevel.Error, Message = "Could not deserialise renderTile request")]
    private partial void LogCouldNotDeserialiseRenderTileRequest(Exception exception);

    [LoggerMessage(Level = LogLevel.Information, Message = "Rendering tile ({PosX}, {PosY}, {Zoom})")]
    private partial void LogRenderingTile(int PosX, int PosY, int Zoom);

    [LoggerMessage(Level = LogLevel.Information, Message = "Sending rendered tile")]
    private partial void LogSendingRenderedTile();

    [LoggerMessage(Level = LogLevel.Critical, Message = "Event bus subscriber error")]
    private partial void LogEventBusSubscriberError(Exception? exception);

    [LoggerMessage(Level = LogLevel.Information, Message = "Started")]
    private partial void LogStarted();
}
