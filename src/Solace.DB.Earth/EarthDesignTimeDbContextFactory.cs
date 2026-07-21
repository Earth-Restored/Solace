using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Solace.DB.Earth;

public sealed class EarthDesignTimeDbContextFactory : IDesignTimeDbContextFactory<EarthDbContext>
{
    public EarthDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<EarthDbContext>();

        optionsBuilder.UseNpgsql("Host=localhost;Database=dummy;");

        return new EarthDbContext(optionsBuilder.Options);
    }
}