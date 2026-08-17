using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Solace.Db.Playfab.Models;
using Solace.Db.Playfab.Models.Items;
using Solace.Db.Playfab.Models.Tabs;

namespace Solace.Db.Playfab;

public sealed class PlayfabDbContext : DbContext
{
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
    public PlayfabDbContext(DbContextOptions<PlayfabDbContext> options)
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
        : base(options)
    {
    }

    public DbSet<SeedingHistory> SeedingHistory { get; set; }

    public DbSet<ItemEF> Items { get; set; }

    public DbSet<ItemDataEF> ItemData { get; set; }

    public DbSet<TabEF> Tabs { get; set; }

    public static PlayfabDbContext CreateFromConnection(string connectionString)
    {
        var optionsBuilder = new DbContextOptionsBuilder<PlayfabDbContext>();
        ConfigureBuilder(optionsBuilder, connectionString);

        return new PlayfabDbContext(optionsBuilder.Options);
    }

    public static void ConfigureBuilder(DbContextOptionsBuilder optionsBuilder, string connectionString)
    {
#pragma warning disable IDE0022 // Use expression body for method
        optionsBuilder.UseNpgsql(connectionString);
#pragma warning restore IDE0022 // Use expression body for method

        // optionsBuilder.UseModel(CompiledModels.PlayfabDbContextModel.Instance);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        var jsonSerializerOptions = new JsonSerializerOptions();

        modelBuilder.Entity<SeedingHistory>(builder =>
        {
            builder.HasKey(e => e.Key);

            builder.Property(e => e.Version)
                .HasConversion(
                    v => v.ToString(),
                    v => Version.Parse(v)
                );
        });

        modelBuilder.Entity<ItemEF>(builder =>
        {
            builder.HasKey(e => e.Id);

            builder.HasOne(a => a.Data)
                .WithOne(b => b.Item)
                .HasForeignKey<ItemDataEF>(a => a.Id)
                .OnDelete(DeleteBehavior.Cascade)
                .IsRequired(false);

            builder.Property(e => e.Keywords)
                .HasConversion(
                    v => JsonSerializer.Serialize(v, jsonSerializerOptions),
                    v => JsonSerializer.Deserialize<Dictionary<string, KeywordValuesEF>>(v, jsonSerializerOptions) ?? new(StringComparer.Ordinal)
                )
                .Metadata.SetValueComparer(new StringDictionaryValueComparer<KeywordValuesEF>(static item => item.DeepCopy()));

            builder.Property(e => e.TitleTranslations)
                .HasConversion(
                    v => JsonSerializer.Serialize(v, jsonSerializerOptions),
                    v => JsonSerializer.Deserialize<Dictionary<string, string>>(v, jsonSerializerOptions) ?? new(StringComparer.Ordinal)
                )
                .Metadata.SetValueComparer(new StringDictionaryValueComparer<string>(static item => item));

            builder.Property(e => e.DescriptionTranslations)
                .HasConversion(
                    v => JsonSerializer.Serialize(v, jsonSerializerOptions),
                    v => JsonSerializer.Deserialize<Dictionary<string, string>>(v, jsonSerializerOptions) ?? new(StringComparer.Ordinal)
                )
                .Metadata.SetValueComparer(new StringDictionaryValueComparer<string>(static item => item));

            builder.Property(e => e.Tags)
                .HasConversion(
                    v => JsonSerializer.Serialize(v, jsonSerializerOptions),
                    v => JsonSerializer.Deserialize<List<string>>(v, jsonSerializerOptions) ?? new()
                )
                .Metadata.SetValueComparer(new ListImmutableValueComparer<string>());

            builder.Property(e => e.ItemReferences)
                .HasConversion(
                    v => JsonSerializer.Serialize(v, jsonSerializerOptions),
                    v => JsonSerializer.Deserialize<List<ItemReferenceEF>>(v, jsonSerializerOptions) ?? new()
                )
                .Metadata.SetValueComparer(new ListValueComparer<ItemReferenceEF>());
        });

        modelBuilder.Entity<ItemDataEF>(builder =>
        {
            builder.HasKey(e => e.Id);

            builder.HasDiscriminator<string>("DataType")
                .HasValue<BuildplateDataEF>("Buildplate")
                .HasValue<InventoryItemDataEF>("InventoryItem");
        });

        modelBuilder.Entity<BuildplateDataEF>()
            .Property(e => e.Version)
                .HasConversion(
                    v => v.ToString(),
                    v => Version.Parse(v)
                );

        modelBuilder.Entity<InventoryItemDataEF>()
            .Property(e => e.Version)
                .HasConversion(
                    v => v.ToString(),
                    v => Version.Parse(v)
                );

        modelBuilder.Entity<TabEF>(builder =>
        {
            builder.HasKey(e => e.TabIndex);

            builder.HasIndex(e => e.TabId)
                .IsUnique();

            builder.Property(e => e.ScreenLayoutQueries)
                .HasConversion(
                    v => JsonSerializer.Serialize(v, jsonSerializerOptions),
                    v => JsonSerializer.Deserialize<List<ScreenLayoutQueryEF>>(v, jsonSerializerOptions) ?? new()
                )
                .Metadata.SetValueComparer(new ListValueComparer<ScreenLayoutQueryEF>());
        });
    }
}
