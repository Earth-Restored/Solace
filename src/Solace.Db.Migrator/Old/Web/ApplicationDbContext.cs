using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Solace.Db.Migrator.Old.Web;

public sealed class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : IdentityDbContext<ApplicationUser, ApplicationRole, string>(options)
{
    public DbSet<DbBuildplatePreview> BuildplatePreviews { get; set; }

    public static ApplicationDbContext CreateFromPath(string path)
        => CreateFromConnection("Data Source=" + Path.GetFullPath(path));

    public static ApplicationDbContext CreateFromConnection(string connectionString)
    {
        var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
        ConfigureBuilder(optionsBuilder, connectionString);

        return new ApplicationDbContext(optionsBuilder.Options);
    }

    public static void ConfigureBuilder(DbContextOptionsBuilder optionsBuilder, string connectionString)
        => optionsBuilder.UseSqlite(connectionString);

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Microsoft.AspNetCore.Identity.IdentityUserPasskey<string>>(b =>
        {
            b.HasKey(p => p.CredentialId);
            b.ToTable("AspNetUserPasskeys");
            b.Property(p => p.CredentialId).HasMaxLength(1024);
            b.ComplexProperty(p => p.Data).ToJson();
        });

        builder.Entity<ApplicationUser>()
            .PrimitiveCollection(e => e.LinkedInGameAccounts);

        builder.Entity<DbBuildplatePreview>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id)
                .ValueGeneratedOnAdd();

            entity.HasIndex(e => new { e.PlayerId, e.BuildplateId })
                .HasDatabaseName("IX_Player_Buildplate")
                .IsUnique();

            entity.Property(e => e.PreviewData)
                .IsRequired()
                .HasColumnType("BLOB");
        });
    }
}
