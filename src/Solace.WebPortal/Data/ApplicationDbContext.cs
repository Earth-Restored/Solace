using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Solace.WebPortal.Data;

public sealed class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : IdentityDbContext<ApplicationUser, ApplicationRole, long>(options)
{
    public DbSet<BuildplatePreviewEF> BuildplatePreviews { get; set; }

    public static ApplicationDbContext CreateFromConnection(string connectionString)
    {
        var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
        ConfigureBuilder(optionsBuilder, connectionString);

        return new ApplicationDbContext(optionsBuilder.Options);
    }

    public static void ConfigureBuilder(DbContextOptionsBuilder optionsBuilder, string connectionString)
    {
#pragma warning disable IDE0022 // Use expression body for method
        optionsBuilder.UseNpgsql(connectionString);
#pragma warning restore IDE0022 // Use expression body for method

        // optionsBuilder.UseModel(CompiledModels.ApplicationDbContext.Instance);
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.UseOpenIddict();

        builder.Entity<BuildplatePreviewEF>(entity =>
        {
            entity.HasKey(bp => new { bp.BuildplateId, bp.PlayerId, });

            entity.Property(e => e.PreviewData)
                .IsRequired()
                .HasColumnType("bytea");
        });
    }

    [Obsolete("Make sure to only call from the DeleteAll endpoint", false)]
    public async Task DeleteAllAsync(CancellationToken cancellationToken = default)
    {
        var ctx = this;  // todo: bug - needed for compiled queries - https://github.com/dotnet/efcore/issues/35887
        var cancellationTokenL = cancellationToken;

        await ctx.BuildplatePreviews.ExecuteDeleteAsync(cancellationTokenL);

        // identity
        await ctx.UserClaims.ExecuteDeleteAsync(cancellationTokenL);
        await ctx.UserLogins.ExecuteDeleteAsync(cancellationTokenL);
        await ctx.UserTokens.ExecuteDeleteAsync(cancellationTokenL);
        await ctx.UserPasskeys.ExecuteDeleteAsync(cancellationTokenL);

        await ctx.RoleClaims.ExecuteDeleteAsync(cancellationTokenL);

        await ctx.UserRoles.ExecuteDeleteAsync(cancellationTokenL);

        await ctx.Users.ExecuteDeleteAsync(cancellationTokenL);
        await ctx.Roles.ExecuteDeleteAsync(cancellationTokenL);
    }
}
