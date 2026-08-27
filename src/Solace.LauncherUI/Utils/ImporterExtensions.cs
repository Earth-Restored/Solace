using System.Diagnostics;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Solace.Buildplate.Model;
using Solace.BuildplateImporter;
using Solace.BuildplateRenderer;
using Solace.DB;
using Solace.DB.Models.Global;
using Solace.DB.Models.Player;
using Solace.EventBus.Client;
using Solace.LauncherUI.Data;
using Solace.LauncherUI.Models.Db;
using Solace.ObjectStore.Client;

namespace Solace.LauncherUI.Utils;

#pragma warning disable CA1708 // Identifiers should differ by more than case
public static class ImporterExtensions
#pragma warning restore CA1708 // Identifiers should differ by more than case
{
    extension(Importer)
    {
        public static async Task<Importer> CreateFromSettings(Settings settings, EarthDbContext earthDb, Serilog.ILogger logger, bool createEventBus = true, bool ownsEarthDb = false)
        {
            var eventBus = createEventBus ? await EventBusClient.ConnectAsync($"localhost:{settings.EventBusPort}") : null;
            var objectStore = await ObjectStoreClient.ConnectAsync($"localhost:{settings.ObjectStorePort}");

            return new Importer(earthDb, eventBus, objectStore, logger)
            {
                OwnsEarthDb = ownsEarthDb,
                OwnsEventBusClient = true,
                OwnsObjectStoreClient = true,
            };
        }
    }

    extension(Importer importer)
    {
        public async Task<ArraySegment<byte>?> GetTemplateLauncherPreviewAsync(Guid templateId, ApplicationDbContext appDbContext, ResourcePackManager resourcePackManager, bool getFromCache = true, CancellationToken cancellationToken = default)
        {
            var dbBuildplatePreview = await appDbContext.BuildplatePreviews
                .AsNoTracking()
                .FirstOrDefaultAsync(preview => preview.PlayerId == null && preview.BuildplateId == templateId, cancellationToken: cancellationToken);

            if (dbBuildplatePreview is not null)
            {
                if (getFromCache)
                {
                    return dbBuildplatePreview.PreviewData;
                }
                else
                {
                    appDbContext.BuildplatePreviews.Remove(dbBuildplatePreview);
                    await appDbContext.SaveChangesAsync(cancellationToken);
                }
            }

            var template = await importer.EarthDB.TemplateBuildplates
                .AsNoTracking()
                .FirstOrDefaultAsync(template => template.Id == templateId, cancellationToken);

            if (template is null)
            {
                importer.Logger.Warning($"Template {templateId} does not exist");
                return null;
            }

            var worldDataRaw = await importer.ObjectStoreClient.GetAsync(template.ServerDataObjectId);

            if (worldDataRaw is null)
            {
                importer.Logger.Error($"Could not get world data for template '{templateId}'");
                return null;
            }

            WorldData? worldData;
            using (var worldDataStream = new MemoryStream(worldDataRaw))
            {
                worldData = await WorldData.LoadFromZipAsync(worldDataStream, importer.Logger, cancellationToken);
            }

            if (worldData is null)
            {
                return null;
            }

            worldData = worldData with { Size = template.Size, Offset = template.Offset, Night = template.Night, };

            var meshGenerator = new BuildplateMeshGenerator(resourcePackManager);

            MeshData? meshData = await meshGenerator.GenerateAsync(worldData, cancellationToken);
            if (meshData is null)
            {
                return null;
            }

            using var ms = new MemoryStream();
            await meshData.ToGlbAsync(resourcePackManager, ms);
            bool getBufferSuccess = ms.TryGetBuffer(out var buffer);
            Debug.Assert(getBufferSuccess);

            dbBuildplatePreview = new DbBuildplatePreview()
            {
                PlayerId = null,
                BuildplateId = templateId,
                PreviewData = [.. buffer],
            };

            return await SaveBuildplatePreviewAsync(appDbContext, dbBuildplatePreview, cancellationToken);
        }

        public async Task<ArraySegment<byte>?> GetPlayerBuildplateLauncherPreviewAsync(Guid accountId, Guid buildplateId, ApplicationDbContext appDbContext, ResourcePackManager resourcePackManager, bool getFromCache = true, CancellationToken cancellationToken = default)
        {
            var dbBuildplatePreview = await appDbContext.BuildplatePreviews
                .AsNoTracking()
                .FirstOrDefaultAsync(preview => preview.PlayerId == accountId && preview.BuildplateId == buildplateId, cancellationToken: cancellationToken);

            if (dbBuildplatePreview is not null)
            {
                if (getFromCache)
                {
                    return dbBuildplatePreview.PreviewData;
                }
                else
                {
                    appDbContext.BuildplatePreviews.Remove(dbBuildplatePreview);
                    await appDbContext.SaveChangesAsync(cancellationToken);
                }
            }

            var buildplate = await importer.EarthDB.PlayerBuildplates
                .AsNoTracking()
                .FirstOrDefaultAsync(buildplate => buildplate.Id == buildplateId && buildplate.AccountId == accountId, cancellationToken);

            if (buildplate is null)
            {
                importer.Logger.Warning($"Player buildplate {buildplateId} does not exist");
                return null;
            }

            var worldDataRaw = await importer.ObjectStoreClient.GetAsync(buildplate.ServerDataObjectId);

            if (worldDataRaw is null)
            {
                importer.Logger.Error($"Could not get world data for buildplate '{buildplate}'");
                return null;
            }

            WorldData? worldData;
            using (var worldDataStream = new MemoryStream(worldDataRaw))
            {
                worldData = await WorldData.LoadFromZipAsync(worldDataStream, importer.Logger, cancellationToken);
            }

            if (worldData is null)
            {
                return null;
            }

            worldData = worldData with { Size = buildplate.Size, Offset = buildplate.Offset, Night = buildplate.Night, };

            var meshGenerator = new BuildplateMeshGenerator(resourcePackManager);

            MeshData? meshData = await meshGenerator.GenerateAsync(worldData, cancellationToken);
            if (meshData is null)
            {
                return null;
            }

            using var ms = new MemoryStream();
            await meshData.ToGlbAsync(resourcePackManager, ms);
            bool getBufferSuccess = ms.TryGetBuffer(out var buffer);
            Debug.Assert(getBufferSuccess);

            dbBuildplatePreview = new DbBuildplatePreview()
            {
                PlayerId = accountId,
                BuildplateId = buildplateId,
                PreviewData = [.. buffer],
            };

            return await SaveBuildplatePreviewAsync(appDbContext, dbBuildplatePreview, cancellationToken);
        }

        private static async Task<ArraySegment<byte>?> SaveBuildplatePreviewAsync(ApplicationDbContext appDbContext, DbBuildplatePreview dbBuildplatePreview, CancellationToken cancellationToken)
        {
            appDbContext.BuildplatePreviews.Add(dbBuildplatePreview);

            try
            {
                await appDbContext.SaveChangesAsync(cancellationToken);
                return dbBuildplatePreview.PreviewData;
            }
            catch (DbUpdateException ex) when (ex.InnerException is SqliteException sqliteEx && sqliteEx.SqliteErrorCode == 19)
            {
                appDbContext.ChangeTracker.Clear();

                var existingPreview = await appDbContext.BuildplatePreviews
                    .AsNoTracking()
                    .FirstOrDefaultAsync(preview => preview.PlayerId == dbBuildplatePreview.PlayerId && preview.BuildplateId == dbBuildplatePreview.BuildplateId, cancellationToken: cancellationToken);

                if (existingPreview is not null)
                {
                    return existingPreview.PreviewData;
                }

                throw;
            }
        }
    }
}