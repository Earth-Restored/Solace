using Microsoft.EntityFrameworkCore;
using Solace.DB.Utils;

namespace Solace.DB.Common;

public static class EarthDbContextExtensions
{
    extension(EarthDbContext earthDb)
    {
        public static EarthDbContext CreateSqliteFromPath(string path)
        => CreateFromConnection("Data Source=" + Path.GetFullPath(path), "Sqlite");

        public static EarthDbContext CreateFromConnection(string connectionString, string provider)
        {
            var optionsBuilder = new DbContextOptionsBuilder<EarthDbContext>();
            ConfigureBuilder(optionsBuilder, connectionString, provider);

            return new EarthDbContext(optionsBuilder.Options);
        }

        public static void ConfigureBuilder(DbContextOptionsBuilder optionsBuilder, string connectionString, string provider)
        {
            switch (provider)
            {
                case "Postgres":
                    optionsBuilder.UseNpgsql(connectionString, x =>
                    {
                        x.MigrationsAssembly("Solace.DB.Postgres");
                    });

                    optionsBuilder.UseModel(CompiledModels.Postgres.EarthDbContextModel.Instance);
                    break;
                case "Sqlite":
                    optionsBuilder.UseSqlite(connectionString, x =>
                    {
                        x.MigrationsAssembly("Solace.DB.Sqlite");
                    });

                    // optionsBuilder.UseModel(CompiledModels.Sqlite.EarthDbContextModel.Instance);
                    break;
                default:
                    throw new ArgumentException($"Unknown db provider '{provider}'.", nameof(provider));
            }

            optionsBuilder.AddInterceptors(new VersioningInterceptor());
        }
    }
}
