using System.Diagnostics;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Solace.Buildplate.Model;
using Solace.BuildplateImporter;
using Solace.BuildplateRenderer;
using Solace.AdminPanel.Data;
using Solace.AdminPanel.Models.Db;
using Solace.Common;
using Solace.Common.Utils;

namespace Solace.AdminPanel.Utils;

#pragma warning disable CA1708 // Identifiers should differ by more than case
internal static class ImporterExtensions
#pragma warning restore CA1708 // Identifiers should differ by more than case
{
    extension(Importer importer)
    {
        public async Task<ArraySegment<byte>?> GetTemplateAdminPanelPreviewAsync(Guid templateId, ApplicationDbContext appDbContext, ResourcePackManager resourcePackManager, IProgress<ProgressReport>? progress = null, bool getFromCache = true, CancellationToken cancellationToken = default)
        {
            var dbBuildplatePreview = await appDbContext.BuildplatePreviews
                .AsNoTracking()
                .FirstOrDefaultAsync(preview => preview.PlayerId == null && preview.BuildplateId == templateId, cancellationToken: cancellationToken);

            if (dbBuildplatePreview is not null)
            {
                if (getFromCache)
                {
                    progress?.Complete();
                    return dbBuildplatePreview.PreviewData;
                }
                else
                {
                    appDbContext.BuildplatePreviews.Remove(dbBuildplatePreview);
                    await appDbContext.SaveChangesAsync(cancellationToken);
                }
            }

            progress?.Report(new ProgressReport(0.005, "Fetching template"));

            var template = await importer.EarthDb.TemplateBuildplates
                .AsNoTracking()
                .FirstOrDefaultAsync(template => template.Id == templateId, cancellationToken);

            if (template is null)
            {
                importer.LogTemplateNotFound(templateId);
                return null;
            }

            progress?.Report(new ProgressReport(0.02, "Fetching template data"));

            using var worldDataRaw = await importer.ObjectStoreClient.GetStreamAsync(template.ServerDataObjectId, cancellationToken);

            if (worldDataRaw is null)
            {
                importer.LogTemplateServerDataLoadError(templateId);
                return null;
            }

            progress?.Report(new ProgressReport(0.05, "Loading template data"));

            var worldData = await WorldData.LoadFromZipAsync(worldDataRaw, importer.Logger, cancellationToken);

            if (worldData is null)
            {
                return null;
            }

            worldData = worldData with { Size = template.Size, Offset = template.Offset, Night = template.Night, };

            var meshGenerator = new BuildplateMeshGenerator(resourcePackManager);

            progress?.Report(new ProgressReport(0.11, "Generating mesh"));

            var meshData = await meshGenerator.GenerateAsync(worldData, progress?.WrapRange(0.11, 0.91), cancellationToken);
            if (meshData is null)
            {
                return null;
            }

            progress?.Report(new ProgressReport(0.91, "Finalizing mesh"));

            using var ms = new MemoryStream();
            await meshData.ToGlbAsync(resourcePackManager, ms);
            var getBufferSuccess = ms.TryGetBuffer(out var buffer);
            Debug.Assert(getBufferSuccess);

            progress?.Complete();

            dbBuildplatePreview = new DbBuildplatePreview()
            {
                PlayerId = null,
                BuildplateId = templateId,
                PreviewData = [.. buffer],
            };

            return await SaveBuildplatePreviewAsync(appDbContext, dbBuildplatePreview, cancellationToken);
        }

        public async Task<ArraySegment<byte>?> GetPlayerBuildplateAdminPanelPreviewAsync(Guid accountId, Guid buildplateId, ApplicationDbContext appDbContext, ResourcePackManager resourcePackManager, IProgress<ProgressReport>? progress = null, bool getFromCache = true, CancellationToken cancellationToken = default)
        {
            var dbBuildplatePreview = await appDbContext.BuildplatePreviews
                .AsNoTracking()
                .FirstOrDefaultAsync(preview => preview.PlayerId == accountId && preview.BuildplateId == buildplateId, cancellationToken: cancellationToken);

            if (dbBuildplatePreview is not null)
            {
                if (getFromCache)
                {
                    progress?.Complete();
                    return dbBuildplatePreview.PreviewData;
                }
                else
                {
                    appDbContext.BuildplatePreviews.Remove(dbBuildplatePreview);
                    await appDbContext.SaveChangesAsync(cancellationToken);
                }
            }

            progress?.Report(new ProgressReport(0.005, "Fetching buildplate"));

            var buildplate = await importer.EarthDb.PlayerBuildplates
                .AsNoTracking()
                .FirstOrDefaultAsync(buildplate => buildplate.Id == buildplateId && buildplate.AccountId == accountId, cancellationToken);

            if (buildplate is null)
            {
                importer.LogBuildplateNotFound(accountId, buildplateId);
                return null;
            }

            progress?.Report(new ProgressReport(0.02, "Fetching template data"));

            using var worldDataRaw = await importer.ObjectStoreClient.GetStreamAsync(buildplate.ServerDataObjectId, cancellationToken);

            if (worldDataRaw is null)
            {
                importer.LogBuildplateServerDataLoadError(accountId, buildplateId);
                return null;
            }

            progress?.Report(new ProgressReport(0.05, "Loading template data"));

            var worldData = await WorldData.LoadFromZipAsync(worldDataRaw, importer.Logger, cancellationToken);

            if (worldData is null)
            {
                return null;
            }

            worldData = worldData with { Size = buildplate.Size, Offset = buildplate.Offset, Night = buildplate.Night, };

            var meshGenerator = new BuildplateMeshGenerator(resourcePackManager);

            progress?.Report(new ProgressReport(0.11, "Generating mesh"));

            MeshData? meshData = await meshGenerator.GenerateAsync(worldData, progress?.WrapRange(0.11, 0.91), cancellationToken);
            if (meshData is null)
            {
                return null;
            }

            progress?.Report(new ProgressReport(0.91, "Finalizing mesh"));

            using var ms = new MemoryStream();
            await meshData.ToGlbAsync(resourcePackManager, ms);
            var getBufferSuccess = ms.TryGetBuffer(out var buffer);
            Debug.Assert(getBufferSuccess);

            progress?.Complete();

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
            catch (DbUpdateException exception) when (ex.InnerException is SqliteException sqliteEx && sqliteEx.SqliteErrorCode == 19)
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