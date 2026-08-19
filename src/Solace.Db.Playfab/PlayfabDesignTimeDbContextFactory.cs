using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Solace.Db.Playfab;

public sealed class PlayfabDesignTimeDbContextFactory : IDesignTimeDbContextFactory<PlayfabDbContext>
{
    public PlayfabDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<PlayfabDbContext>();

        optionsBuilder.UseNpgsql("Host=localhost;Database=dummy;");

        return new PlayfabDbContext(optionsBuilder.Options);
    }
}