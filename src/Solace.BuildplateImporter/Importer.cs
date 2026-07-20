using System.Text;
using System.Text.Json;
using Solace.Buildplate.Model;
using Solace.Common.Utils;
using Solace.DB;
using Solace.DB.Models.Global;
using Solace.DB.Models.Player;
using Solace.EventBus.Client;
using Solace.ObjectStore.Client;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Solace.BuildplateImporter;

public sealed partial class Importer : IAsyncDisposable
{
    public readonly EarthDbContext EarthDB;
    public readonly EventBusClient? EventBusClient;
    public readonly ObjectStoreClient ObjectStoreClient;
    public readonly ILogger Logger;

    public Importer(EarthDbContext earthDB, EventBusClient? eventBusClient, ObjectStoreClient objectStoreClient, ILogger logger)
    {
        EarthDB = earthDB;
        EventBusClient = eventBusClient;
        ObjectStoreClient = objectStoreClient;
        Logger = logger;
    }

    public required bool OwnsEarthDb { get; init; }

    public required bool OwnsEventBusClient { get; init; }

    public required bool OwnsObjectStoreClient { get; init; }

    public async Task<bool> ImportTemplateAsync(Guid templateId, string name, Stream stream, CancellationToken cancellationToken = default)
    {
        var worldData = await WorldData.LoadFromZipAsync(stream, Logger, cancellationToken);

        if (worldData is null)
        {
            return false;
        }

        var preview = await GeneratePreview(worldData);

        if (preview is null)
        {
            return false;
        }

        return await StoreTemplate(templateId, name, preview, worldData, cancellationToken);
    }

    public async Task<Stream?> RegenerateTemplatePreviewAsync(Guid templateId, CancellationToken cancellationToken = default)
    {
        TemplateBuildplateEF? template;
        try
        {
            template = await EarthDB.TemplateBuildplates
                .AsTracking()
                .FirstOrDefaultAsync(template => template.Id == templateId, cancellationToken);
        }
        catch (Exception exception)
        {
            LogTemplateFetchError(exception, templateId);
            return null;
        }

        if (template is null)
        {
            LogTemplateNotFound(templateId);
            return null;
        }

        if (Guid.IsNullOrZero(template.ServerDataObjectId))
        {
            LogTemplateNoAssociatedServerData(templateId);
            return null;
        }

        using var serverData = await ObjectStoreClient.GetStreamAsync(template.ServerDataObjectId, cancellationToken);

        if (serverData is null)
        {
            LogTemplateServerDataLoadError(templateId);
            return null;
        }

        var worldData = await WorldData.LoadFromZipAsync(serverData, Logger, cancellationToken);

        if (worldData is null)
        {
            return null;
        }

        worldData = worldData with { Size = template.Size, Offset = template.Offset, Night = template.Night, };

        var preview = await GeneratePreview(worldData);

        if (preview is null)
        {
            return null;
        }

        var newPreviewObjectId = await ObjectStoreClient.StoreAsync(preview, cancellationToken);
        if (newPreviewObjectId is null)
        {
            LogTemplatePreviewStoreFail(templateId);
            return null;
        }

        var oldPreviewObjectId = template.PreviewObjectId;

        template.PreviewObjectId = newPreviewObjectId.Value;

        try
        {
            await EarthDB.SaveChangesAsync(cancellationToken);

            if (!Guid.IsNullOrZero(oldPreviewObjectId))
            {
                await ObjectStoreClient.DeleteAsync(oldPreviewObjectId, cancellationToken);
                LogDeletedOldTemplatePreview(templateId);
            }

            return preview;
        }
        catch (Exception exception)
        {
            LogTemplatePreviewSaveFail(exception, templateId);
            await ObjectStoreClient.DeleteAsync(newPreviewObjectId.Value, cancellationToken);
            return null;
        }
    }

    public async Task<bool> RemoveTemplateAsync(Guid templateId, bool removeFromPlayers, CancellationToken cancellationToken = default)
    {
        LogRemovingTemplate(templateId);

        TemplateBuildplateEF? template;
        try
        {
            template = await EarthDB.TemplateBuildplates
                .AsTracking()
                .FirstOrDefaultAsync(template => template.Id == templateId, cancellationToken);
        }
        catch (Exception exception)
        {
            LogTemplateFetchError(exception, templateId);
            return false;
        }

        if (template is null)
        {
            LogTemplateNotFound(templateId);
            return true;
        }

        if (removeFromPlayers)
        {
            List<PlayerBuildplateEF> instances;

            try
            {
                instances = await EarthDB.PlayerBuildplates
                    .AsNoTracking()
                    .Where(buildplate => buildplate.TemplateId == templateId)
                    .ToListAsync(cancellationToken);
            }
            catch (Exception exception)
            {
                LogGetBuildplatesBasedOnTemplateError(exception, templateId);
                return false;
            }

            LogPlayerBuildplateToRemoveCount(instances.Count);

            foreach (var buildplate in instances)
            {
                await RemoveBuildplateFromPlayer(buildplate.Id, buildplate.AccountId, cancellationToken);
            }
        }

        try
        {
            EarthDB.TemplateBuildplates.Remove(template);

            await EarthDB.SaveChangesAsync(cancellationToken);
        }
        catch (Exception exception)
        {
            LogRemoveTemplateFail(exception, templateId);
            return false;
        }

        if (!Guid.IsNullOrZero(template.ServerDataObjectId))
        {
            await ObjectStoreClient.DeleteAsync(template.ServerDataObjectId, cancellationToken);
        }

        if (!Guid.IsNullOrZero(template.PreviewObjectId))
        {
            await ObjectStoreClient.DeleteAsync(template.PreviewObjectId, cancellationToken);
        }

        if (removeFromPlayers)
        {
            LogRemovedTemplateFromPlayers(templateId);
        }
        else
        {
            LogRemovedTemplate(templateId);
        }

        return true;
    }

    public async Task<Guid?> AddBuidplateToPlayer(Guid templateId, Guid playerId, CancellationToken cancellationToken = default)
    {
        TemplateBuildplateEF? template;
        try
        {
            template = await EarthDB.TemplateBuildplates
                .AsNoTracking()
                .FirstOrDefaultAsync(template => template.Id == templateId, cancellationToken);
        }
        catch (Exception exception)
        {
            LogTemplateFetchError(exception, templateId);
            return null;
        }

        if (template is null)
        {
            LogTemplateNotFound(templateId);
            return null;
        }

        var serverData = await ObjectStoreClient.GetArrayAsync(template.ServerDataObjectId, cancellationToken);

        if (serverData is null)
        {
            LogTemplateServerDataLoadError(templateId);
            return null;
        }

        var preview = await ObjectStoreClient.GetStreamAsync(template.PreviewObjectId, cancellationToken);

        if (preview is null)
        {
            LogTemplatePreviewLoadError(LogLevel.Warning, templateId);
            preview = await GeneratePreview(new WorldData(serverData, template.Size, template.Offset, template.Night));

            if (preview is null)
            {
                return null;
            }
        }

        var buidplateId = Guid.CreateVersion7();

        if (!await StoreBuildplate(templateId, playerId, buidplateId, template, serverData, preview, cancellationToken))
        {
            await preview.DisposeAsync();
            return null;
        }

        await preview.DisposeAsync();

        return buidplateId;
    }

    public async Task<bool> RegeneratePlayerBuildplatePreviewAsync(Guid accountId, Guid buildplateId, CancellationToken cancellationToken = default)
    {
        PlayerBuildplateEF? buildplate;

        try
        {
            buildplate = await EarthDB.PlayerBuildplates
                .AsTracking()
                .FirstOrDefaultAsync(buildplate => buildplate.Id == buildplateId && buildplate.AccountId == accountId, cancellationToken);
        }
        catch (Exception exception)
        {
            LogBuildplateFetchError(exception, accountId, buildplateId);
            return false;
        }

        if (buildplate is null)
        {
            LogBuildplateNotFound(accountId, buildplateId);
            return false;
        }

        if (Guid.IsNullOrZero(buildplate.ServerDataObjectId))
        {
            LogBuildplateNoAssociatedServerData(accountId, buildplateId);
            return false;
        }

        using var serverData = await ObjectStoreClient.GetStreamAsync(buildplate.ServerDataObjectId, cancellationToken);

        if (serverData is null)
        {
            LogBuildplateServerDataLoadError(accountId, buildplateId);
            return false;
        }

        WorldData? worldData = await WorldData.LoadFromZipAsync(serverData, Logger, cancellationToken);

        if (worldData is null)
        {
            return false;
        }

        worldData = worldData with { Size = buildplate.Size, Offset = buildplate.Offset, Night = buildplate.Night, };

        var preview = await GeneratePreview(worldData);

        if (preview is null)
        {
            return false;
        }

        var newPreviewObjectId = await ObjectStoreClient.StoreAsync(preview, cancellationToken);
        if (newPreviewObjectId is null)
        {
            LogBuildplatePreviewStoreFail(accountId, buildplateId);
            return false;
        }

        var oldPreviewObjectId = buildplate.PreviewObjectId;

        buildplate.PreviewObjectId = newPreviewObjectId.Value;

        try
        {
            await EarthDB.SaveChangesAsync(cancellationToken);

            if (!Guid.IsNullOrZero(oldPreviewObjectId))
            {
                await ObjectStoreClient.DeleteAsync(oldPreviewObjectId, cancellationToken);
                LogDeletedOldBuildplatePreview(accountId, buildplateId);
            }

            return true;
        }
        catch (Exception exception)
        {
            LogBuildplatePreviewSaveFail(exception, accountId, buildplateId);
            await ObjectStoreClient.DeleteAsync(newPreviewObjectId.Value, cancellationToken);
            return false;
        }
    }

    public async Task<bool> RemoveBuildplateFromPlayer(Guid buildplateId, Guid accountId, CancellationToken cancellationToken = default)
    {
        LogRemovingBuildplate(accountId, buildplateId);

        try
        {
            var buildplate = await EarthDB.PlayerBuildplates
                .AsTracking()
                .FirstOrDefaultAsync(buildplate => buildplate.Id == buildplateId && buildplate.AccountId == accountId, cancellationToken);

            if (buildplate is null)
            {
                LogBuildplateNotFound(accountId, buildplateId);
                return true;
            }

            EarthDB.PlayerBuildplates.Remove(buildplate);
            await EarthDB.SaveChangesAsync(cancellationToken);

            if (!Guid.IsNullOrZero(buildplate.ServerDataObjectId))
            {
                LogDeletingServerDataObject(buildplate.ServerDataObjectId);
                await ObjectStoreClient.DeleteAsync(buildplate.ServerDataObjectId, cancellationToken);
            }

            if (!Guid.IsNullOrZero(buildplate.PreviewObjectId))
            {
                LogDeletingPreviewObject(buildplate.PreviewObjectId);
                await ObjectStoreClient.DeleteAsync(buildplate.PreviewObjectId, cancellationToken);
            }

            return true;
        }
        catch (Exception exception) when (exception is DbUpdateException or DbUpdateConcurrencyException)
        {
            LogRemoveBuildplateFail(exception, accountId, buildplateId);
            return false;
        }
        catch (Exception exception)
        {
            LogRemoveBuildplateFail(exception, accountId, buildplateId);
            return false;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (OwnsEarthDb)
        {
            EarthDB.Dispose();
        }

        if (OwnsEventBusClient && EventBusClient is not null)
        {
            await EventBusClient.DisposeAsync();
        }

        if (OwnsObjectStoreClient)
        {
            await ObjectStoreClient.DisposeAsync();
        }
    }

    private async Task<Stream?> GeneratePreview(WorldData worldData)
    {
        string? preview;
        if (EventBusClient is not null)
        {
            LogGeneratingPreview();
            await using RequestSender requestSender = await EventBusClient.AddRequestSenderAsync();
            preview = await requestSender.RequestAsync("buildplates", "preview", JsonSerializer.Serialize(new PreviewRequest(Convert.ToBase64String(worldData.ServerData), worldData.Night)));

            if (preview is null)
            {
                LogGeneratePreviewFailNoResponse();
            }
        }
        else
        {
            LogGeneratePreviewSkippedNotConnected();
            preview = null;
        }

        return preview is not null ? new MemoryStream(Encoding.ASCII.GetBytes(preview)) : null;
    }

    private async Task<bool> StoreTemplate(Guid templateId, string name, Stream preview, WorldData worldData, CancellationToken cancellationToken)
    {
        TemplateBuildplateEF? template;
        try
        {
            template = await EarthDB.TemplateBuildplates
                .AsNoTracking()
                .FirstOrDefaultAsync(template => template.Id == templateId, cancellationToken);
        }
        catch (Exception exception)
        {
            LogTemplateFetchError(exception, templateId);
            return false;
        }

        if (template is not null)
        {
            LogTemplateAlreadyExists(templateId);
            return false;
            /*_logger.Information("Template buildplate found, updating");

            _logger.Information("Storing template world");
            string? serverDataObjectId = (string?)await objectStoreClient.Store(worldData.ServerData).Task;
            if (serverDataObjectId is null)
            {
                _logger.Error("Could not store template data object in object store");
                return false;
            }

            _logger.Information("Storing template preview");
            string? previewObjectId = (string?)await objectStoreClient.Store(preview).Task;
            if (previewObjectId is null)
            {
                _logger.Error("Could not store template preview object in object store");
                return false;
            }

            _logger.Information("Updating template object ids");
            string oldDataObjectId = template.ServerDataObjectId;
            string oldPreviewObjectId = template.PreviewObjectId;

            template = template with
            {
                ServerDataObjectId = serverDataObjectId,
                PreviewObjectId = previewObjectId
            };

            try
            {
                var results = await new EarthDB.ObjectQuery(true)
                   .UpdateBuildplate(templateId, template)
                   .ExecuteAsync(earthDB, cancellationToken);
            }
            catch (EarthDB.DatabaseException ex)
            {
                _logger.Error($"Failed to update template buildplate: {ex}");
                return false;
            }

            _logger.Information("Deleting old template objects");
            await objectStoreClient.Delete(oldDataObjectId).Task;
            await objectStoreClient.Delete(oldPreviewObjectId).Task;*/
        }
        else
        {
            LogTemplateNotFoundDebug(templateId);

            LogStoringTemplateWorldData();
            Guid? serverDataObjectId;
            await using (var serverDataStream = new MemoryStream(worldData.ServerData))
            {
                serverDataObjectId = await ObjectStoreClient.StoreAsync(serverDataStream, cancellationToken);
            }

            if (serverDataObjectId is null)
            {
                LogTemplateServerDataStoreFail(templateId);
                return false;
            }

            LogStoringTemplatePreview();
            Guid? previewObjectId = await ObjectStoreClient.StoreAsync(preview, cancellationToken);
            if (previewObjectId is null)
            {
                LogTemplatePreviewStoreFail(templateId);
                return false;
            }

            var scale = worldData.Size switch
            {
                8 => 14,
                16 => 33,
                32 => 64,
                _ => 33,
            };

            template = new TemplateBuildplateEF()
            {
                Id = templateId,
                Name = name,
                Size = worldData.Size,
                Offset = worldData.Offset,
                BlocksPerMeter = scale,
                Night = worldData.Night,
                ServerDataObjectId = serverDataObjectId.Value,
                PreviewObjectId = previewObjectId.Value,
            };

            try
            {
                EarthDB.TemplateBuildplates.Add(template);
                await EarthDB.SaveChangesAsync(cancellationToken);
            }
            catch (Exception exception)
            {
                LogTemplateSaveFail(exception, templateId);
                await ObjectStoreClient.DeleteAsync(serverDataObjectId.Value, cancellationToken);
                await ObjectStoreClient.DeleteAsync(previewObjectId.Value, cancellationToken);
                return false;
            }
        }

        return true;
    }

    private async Task<bool> StoreBuildplate(Guid templateId, Guid accountId, Guid buildplateId, TemplateBuildplateEF template, byte[] serverData, Stream preview, CancellationToken cancellationToken)
    {
        LogStoringServerData();
        Guid? serverDataObjectId;
        using (var serverDataStream = new MemoryStream(serverData))
        {
            serverDataObjectId = await ObjectStoreClient.StoreAsync(serverDataStream, cancellationToken);
        }

        if (serverDataObjectId is null)
        {
            LogBuildplateServerDataStoreFail(accountId, buildplateId);
            return false;
        }

        LogStoringPreview();
        var previewObjectId = await ObjectStoreClient.StoreAsync(preview, cancellationToken);
        if (previewObjectId is null)
        {
            LogBuildplatePreviewStoreFail(accountId, buildplateId);
            await ObjectStoreClient.DeleteAsync(serverDataObjectId.Value, cancellationToken);
            return false;
        }

        try
        {
            var lastModified = DateTimeOffset.UtcNow;

            EarthDB.PlayerBuildplates.Add(new PlayerBuildplateEF()
            {
                Id = buildplateId,
                AccountId = accountId,
                TemplateId = templateId,
                Name = template.Name,
                Size = template.Size,
                Offset = template.Offset,
                BlocksPerMeter = template.BlocksPerMeter,
                Night = template.Night,
                LastModified = lastModified,
                ServerDataObjectId = serverDataObjectId.Value,
                PreviewObjectId = previewObjectId.Value,
            });

            await EarthDB.SaveChangesAsync(cancellationToken);

            return true;
        }
        catch (Exception exception)
        {
            LogBuildplateSaveFail(exception, accountId, buildplateId);
            await ObjectStoreClient.DeleteAsync(serverDataObjectId.Value, cancellationToken);
            await ObjectStoreClient.DeleteAsync(previewObjectId.Value, cancellationToken);
            return false;
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to fetch template '{TemplateId}' from db")]
    private partial void LogTemplateFetchError(Exception exception, Guid TemplateId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to fetch buildplate '{BuildplateId}' for player '{AccountId}' from db")]
    private partial void LogBuildplateFetchError(Exception exception, Guid AccountId, Guid BuildplateId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Template '{TemplateId}' does not exist")]
    public partial void LogTemplateNotFound(Guid TemplateId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Buildplate '{BuildplateId}' for player '{AccountId}' does not exist")]
    public partial void LogBuildplateNotFound(Guid AccountId, Guid BuildplateId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to get world data for template '{TemplateId}'")]
    public partial void LogTemplateServerDataLoadError(Guid TemplateId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to get world data for buildplate '{BuildplateId}' for player '{AccountId}'")]
    public partial void LogBuildplateServerDataLoadError(Guid AccountId, Guid BuildplateId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to store world data for template '{TemplateId}'")]
    private partial void LogTemplateServerDataStoreFail(Guid TemplateId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to store world data for buildplate '{BuildplateId}' for player '{AccountId}'")]
    private partial void LogBuildplateServerDataStoreFail(Guid AccountId, Guid BuildplateId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to store preview for template '{TemplateId}'")]
    private partial void LogTemplatePreviewStoreFail(Guid TemplateId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to store preview for buildplate '{BuildplateId}' for player '{AccountId}'")]
    private partial void LogBuildplatePreviewStoreFail(Guid AccountId, Guid BuildplateId);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Deleted old preview for template '{TemplateId}'")]
    private partial void LogDeletedOldTemplatePreview(Guid TemplateId);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Deleted old preview for buildplate '{BuildplateId}' for player '{AccountId}'")]
    private partial void LogDeletedOldBuildplatePreview(Guid AccountId, Guid BuildplateId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to save preview to db for template '{TemplateId}'")]
    private partial void LogTemplatePreviewSaveFail(Exception exception, Guid TemplateId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to save preview to db for buildplate '{BuildplateId}' for player '{AccountId}'")]
    private partial void LogBuildplatePreviewSaveFail(Exception exception, Guid AccountId, Guid BuildplateId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Removing template '{TemplateId}'")]
    private partial void LogRemovingTemplate(Guid TemplateId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Removing buildplate '{BuildplateId}' for player '{AccountId}'")]
    private partial void LogRemovingBuildplate(Guid AccountId, Guid BuildplateId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Error getting buildplates based on template '{TemplateId}'")]
    private partial void LogGetBuildplatesBasedOnTemplateError(Exception exception, Guid TemplateId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Found {PlayerBuildplateCount} player buildplates to remove")]
    private partial void LogPlayerBuildplateToRemoveCount(int PlayerBuildplateCount);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to remove template '{TemplateId}' from db")]
    private partial void LogRemoveTemplateFail(Exception exception, Guid TemplateId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to remove buildplate '{BuildplateId}' for player '{AccountId}' from db")]
    private partial void LogRemoveBuildplateFail(Exception exception, Guid AccountId, Guid BuildplateId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Removed template '{TemplateId}'")]
    private partial void LogRemovedTemplate(Guid TemplateId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Removed template '{TemplateId}', and associated player buildplates")]
    private partial void LogRemovedTemplateFromPlayers(Guid TemplateId);

    [LoggerMessage(Message = "Could not get preview for template '{TemplateId}'")]
    private partial void LogTemplatePreviewLoadError(LogLevel logLevel, Guid TemplateId);

    [LoggerMessage(Message = "Could not get preview for template '{BuildplateId}'")]
    private partial void LogBuildplatePreviewLoadError(LogLevel logLevel, Guid BuildplateId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Generating preview")]
    private partial void LogGeneratingPreview();

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not get preview for buildplate (preview generator did not respond to event bus request)")]
    private partial void LogGeneratePreviewFailNoResponse();

    [LoggerMessage(Level = LogLevel.Warning, Message = "Preview was not generated because event bus is not connected")]
    private partial void LogGeneratePreviewSkippedNotConnected();

    [LoggerMessage(Level = LogLevel.Information, Message = "Deleting world data object '{ServerDataObjectId}'")]
    private partial void LogDeletingServerDataObject(Guid ServerDataObjectId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Deleting preview object '{PreviewObjectId}'")]
    private partial void LogDeletingPreviewObject(Guid PreviewObjectId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Template '{TemplateId}' already exists")]
    private partial void LogTemplateAlreadyExists(Guid TemplateId);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Template '{TemplateId}' not found")]
    private partial void LogTemplateNotFoundDebug(Guid TemplateId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Storing template world data")]
    private partial void LogStoringTemplateWorldData();

    [LoggerMessage(Level = LogLevel.Information, Message = "Storing template preview")]
    private partial void LogStoringTemplatePreview();

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to save template '{TemplateId}' to db")]
    private partial void LogTemplateSaveFail(Exception exception, Guid TemplateId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to save buildplate '{BuildplateId}' for player '{AccountId}' to db")]
    private partial void LogBuildplateSaveFail(Exception exception, Guid AccountId, Guid BuildplateId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Storing world data")]
    private partial void LogStoringServerData();

    [LoggerMessage(Level = LogLevel.Information, Message = "Storing preview")]
    private partial void LogStoringPreview();

    [LoggerMessage(Level = LogLevel.Error, Message = "Template '{TemplateId}' has no associated world data")]
    private partial void LogTemplateNoAssociatedServerData(Guid TemplateId);

    [LoggerMessage(Level = LogLevel.Error, Message = "'{AccountId}''s buildplate '{BuildplateId}' has no associated world data")]
    private partial void LogBuildplateNoAssociatedServerData(Guid AccountId, Guid BuildplateId);
}