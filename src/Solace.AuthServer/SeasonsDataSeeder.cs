using Solace.AuthServer.Utils;
using Solace.Db;
using Solace.Db.Earth;
using Solace.StaticData;

namespace Solace.AuthServer;

public sealed class SeasonsDataSeeder(IServiceProvider serviceProvider) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using var scope = serviceProvider.CreateAsyncScope();
        var earthDb = scope.ServiceProvider.GetRequiredService<EarthDbContext>();
        await earthDb.Database.MigrateAsyncWithLock(cancellationToken);

        var staticData = scope.ServiceProvider.GetRequiredService<StaticDataProvider>();

        await DataSeedUtils.SeedSeasonsDataAsync(earthDb, staticData.Seasons, update: false, force: false, cancellationToken);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
    }
}
