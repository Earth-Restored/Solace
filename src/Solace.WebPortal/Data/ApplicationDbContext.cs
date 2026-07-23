using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Solace.WebPortal.Data;

public sealed class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : IdentityDbContext<ApplicationUser, ApplicationRole, long>(options)
{
    public DbSet<BuildplatePreviewEF> BuildplatePreviews { get; set; }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<BuildplatePreviewEF>(entity =>
        {
            entity.HasKey(bp => new { bp.BuildplateId, bp.PlayerId, });

            entity.Property(e => e.PreviewData)
                .IsRequired()
                .HasColumnType("bytea");
        });
    }
}
