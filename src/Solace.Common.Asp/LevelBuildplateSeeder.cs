using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Solace.BuildplateImporter;
using Solace.Db.Earth;
using Solace.EventBus.Client;
using Solace.ObjectStore.Client;

namespace Solace.Common.Asp;

public static partial class LevelBuildplateSeeder
{
    public static async Task SeedLevelBuildplates(Guid profileId, EarthDbContext earthDb, EventBusClient? eventBus, ObjectStoreClient objectStore, StaticData.Buildplates buildplates, ILogger logger, CancellationToken cancellationToken = default)
    {
        await using var transaction = await earthDb.Database.BeginTransactionAsync(cancellationToken);

        Importer? importer = null;

        foreach (var staticBuildplate in buildplates.LevelBuildplates)
        {
            if (await earthDb.PlayerBuildplates
                .AsTracking()
                .AnyAsync(buildplate => buildplate.ProfileId == profileId && buildplate.TemplateId == staticBuildplate.Id, cancellationToken))
            {
                continue;
            }

            var buildplateInfo = staticBuildplate.GetInfo();

            importer ??= new Importer(earthDb, eventBus, objectStore, logger)
            {
                OwnsEarthDb = false,
                OwnsEventBusClient = false,
                OwnsObjectStoreClient = false,
            };

            var dbBuildplate = await importer.AddBuidplateToPlayer(staticBuildplate.Id, profileId, cancellationToken);

            if (dbBuildplate is null)
            {
                LogFailedToAdd(logger, staticBuildplate.Id, profileId);
                continue;
            }
        }

        if (importer is not null)
        {
            await importer.DisposeAsync();
        }

        await transaction.CommitAsync(cancellationToken);
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to add level up template {TemplateId} to profile {ProfileId}")]
    private static partial void LogFailedToAdd(ILogger logger, Guid TemplateId, Guid ProfileId);
}
