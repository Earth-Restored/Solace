using System.Diagnostics;
using System.Numerics;
using Microsoft.EntityFrameworkCore;
using Solace.Buildplate.Model;
using Solace.BuildplateImporter;
using Solace.BuildplateRenderer;
using Solace.Common;
using Solace.Common.Utils;
using Solace.Db.Earth.Utils;
using Solace.WebPortal.Data;

namespace Solace.WebPortal.Features.Buildplates;

internal static class ImporterExtensions
{
    extension(Importer importer)
    {
        public async Task<(byte[] Data, Vector3 BoundsMin, Vector3 BoundsMax)?> GenerateTemplateWebPortalPreviewAsync(Guid templateId, ApplicationDbContext appDbContext, ResourcePackManager resourcePackManager, IProgress<ProgressReport>? progress = null, CancellationToken cancellationToken = default)
        {
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

            var dbBuildplatePreview = new BuildplatePreviewEF()
            {
                PlayerId = Guid.Empty,
                BuildplateId = templateId,
                PreviewData = [.. buffer],
                BoundsMin = meshData.BoundsMin,
                BoundsMax = meshData.BoundsMax,
            };

            return await SaveBuildplatePreviewAsync(appDbContext, dbBuildplatePreview, cancellationToken);
        }

        public async Task<(byte[] Data, Vector3 BoundsMin, Vector3 BoundsMax)?> GetPlayerBuildplateWebPortalPreviewAsync(Guid accountId, Guid buildplateId, ApplicationDbContext appDbContext, ResourcePackManager resourcePackManager, IProgress<ProgressReport>? progress = null, CancellationToken cancellationToken = default)
        {
            progress?.Report(new ProgressReport(0.005, "Fetching buildplate"));

            var buildplate = await importer.EarthDb.PlayerBuildplates
                .AsNoTracking()
                .FirstOrDefaultAsync(buildplate => buildplate.Id == buildplateId && buildplate.ProfileId == accountId, cancellationToken);

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

            var dbBuildplatePreview = new BuildplatePreviewEF()
            {
                PlayerId = accountId,
                BuildplateId = buildplateId,
                PreviewData = [.. buffer],
                BoundsMin = meshData.BoundsMin,
                BoundsMax = meshData.BoundsMax,
            };

            return await SaveBuildplatePreviewAsync(appDbContext, dbBuildplatePreview, cancellationToken);
        }

        private static async Task<(byte[] Data, Vector3 BoundsMin, Vector3 BoundsMax)> SaveBuildplatePreviewAsync(ApplicationDbContext appDbContext, BuildplatePreviewEF dbBuildplatePreview, CancellationToken cancellationToken)
        {
            appDbContext.BuildplatePreviews.Add(dbBuildplatePreview);

            try
            {
                await appDbContext.SaveChangesAsync(cancellationToken);
                return (dbBuildplatePreview.PreviewData, dbBuildplatePreview.BoundsMin, dbBuildplatePreview.BoundsMax);
            }
            catch (DbUpdateException exception) when (exception.IsUniqueConstraintViolation)
            {
                appDbContext.ChangeTracker.Clear();

                var existingPreview = await appDbContext.BuildplatePreviews
                    .AsNoTracking()
                    .FirstOrDefaultAsync(preview => preview.PlayerId == dbBuildplatePreview.PlayerId && preview.BuildplateId == dbBuildplatePreview.BuildplateId, cancellationToken: cancellationToken);

                if (existingPreview is not null)
                {
                    return (existingPreview.PreviewData, existingPreview.BoundsMin, existingPreview.BoundsMax);
                }

                throw;
            }
        }
    }
}
