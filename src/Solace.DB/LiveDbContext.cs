using Microsoft.EntityFrameworkCore;
using Solace.DB.Models;

namespace Solace.DB;

#pragma warning disable IL2026 // not used from AOT projects
#pragma warning disable IL3050 // not used from AOT projects
[Obsolete("Use EarthDbContext instead.")]
public class LiveDbContext : DbContext
{
    public LiveDbContext(DbContextOptions<LiveDbContext> options)
        : base(options)
    {
    }

    public DbSet<Account.Legacy> Accounts { get; set; }
}
#pragma warning restore IL3050
#pragma warning restore IL2026
