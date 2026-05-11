using Microsoft.EntityFrameworkCore;
using Solace.DB.Models;

namespace Solace.DB;

public class LiveDbContext : DbContext
{
    public LiveDbContext(DbContextOptions<LiveDbContext> options)
        : base(options)
    {
    }

    public DbSet<Account.Legacy> Accounts { get; set; }
}
