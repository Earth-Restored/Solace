using Solace.AuthServer.Utils;
using Solace.Db;
using Solace.Db.Playfab;
using Solace.StaticData;

namespace Solace.AuthServer;

public sealed class PlayfabDataSeeder(IServiceProvider serviceProvider) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using var scope = serviceProvider.CreateAsyncScope();
        var playfabDb = scope.ServiceProvider.GetRequiredService<PlayfabDbContext>();
        await playfabDb.Database.MigrateAsyncWithLock(cancellationToken);

        var staticData = scope.ServiceProvider.GetRequiredService<StaticDataProvider>();

        await DataSeedUtils.SeedPlayfabDataAsync(playfabDb, staticData.Playfab, update: false, force: false, cancellationToken);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
    }
}
