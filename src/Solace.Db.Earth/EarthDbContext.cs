using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Solace.Db.Earth.Models;
using Solace.Db.Earth.Models.Global;
using Solace.Db.Earth.Models.Player;
using Solace.Db.Earth.Models.Player.Workshop;
using Solace.Db.Earth.Utils;

namespace Solace.Db.Earth;

public sealed class EarthDbContext : DbContext
{
#pragma warning disable IL2026
#pragma warning disable IL3050
    public EarthDbContext(DbContextOptions<EarthDbContext> options)
        : base(options)
    {
    }
#pragma warning restore IL3050
#pragma warning restore IL2026

    public DbSet<ProfileEF> Profiles { get; set; }

    public DbSet<ProfileVersions> ProfileVersions { get; set; }

    public DbSet<ActivityLogEntryEF> ActivityLogs { get; set; }

    public DbSet<BoostsEF> Boosts { get; set; }

    public DbSet<PlayerBuildplateEF> PlayerBuildplates { get; set; }

    public DbSet<HotbarEF> Hotbars { get; set; }

    public DbSet<StackableItemEF> StackableItems { get; set; }

    public DbSet<NonStackableItemInstanceEF> NonStackableItems { get; set; }

    public DbSet<ItemJournalEntryEF> JournalEntries { get; set; }

    public DbSet<RedeemedTappableEF> RedeemedTappables { get; set; }

    public DbSet<TokenEF> Tokens { get; set; }

    public DbSet<CraftingSlotsEF> CraftingSlots { get; set; }

    public DbSet<SmeltingSlotsEF> SmeltingSlots { get; set; }

    public DbSet<EncounterBuildplateEF> EncounterBuildplates { get; set; }

    public DbSet<SharedBuildplateEF> SharedBuildplates { get; set; }

    public DbSet<TemplateBuildplateEF> TemplateBuildplates { get; set; }

    public DbSet<Tile> Tiles { get; set; }

    public DbSet<Secret> Secrets { get; set; }

    public static EarthDbContext CreateFromConnection(string connectionString)
    {
        var optionsBuilder = new DbContextOptionsBuilder<EarthDbContext>();
        ConfigureBuilder(optionsBuilder, connectionString);

        return new EarthDbContext(optionsBuilder.Options);
    }

    public static void ConfigureBuilder(DbContextOptionsBuilder optionsBuilder, string connectionString)
    {
        optionsBuilder.UseNpgsql(connectionString);

        // optionsBuilder.UseModel(CompiledModels.EarthDbContextModel.Instance);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // ToJson gives these
#pragma warning disable CS8634 // The type cannot be used as type parameter in the generic type or method. Nullability of type argument doesn't match 'class' constraint.
#pragma warning disable IDE0058 // Expression value is never used
#pragma warning disable CS8621 // Nullability of reference types in return type doesn't match the target delegate (possibly because of nullability attributes).
#pragma warning disable CS8622 // Nullability of reference types in type of parameter doesn't match the target delegate (possibly because of nullability attributes).
        base.OnModelCreating(modelBuilder);

        // triggers
        // todo: bug when compiling - The given key 'EntityType: TokenEF Abstract' was not present in the dictionary.
        // modelBuilder.Entity<ProfileEF>(entity =>
        // {
        //     entity.ToTable("Profiles", tb => tb.HasTrigger("trg_profiles_version"));
        // });

        // modelBuilder.Entity<StackableItemEF>(entity =>
        // {
        //     entity.ToTable("StackableItems", tb => tb.HasTrigger("trg_stackable_items_version"));
        // });

        // modelBuilder.Entity<NonStackableItemInstanceEF>(entity =>
        // {
        //     entity.ToTable("NonStackableItems", tb => tb.HasTrigger("trg_non_stackable_items_version"));
        // });

        // modelBuilder.Entity<CraftingSlotsEF>(entity =>
        // {
        //     entity.ToTable("CraftingSlots", tb => tb.HasTrigger("trg_crafting_slots_version"));
        // });

        // modelBuilder.Entity<SmeltingSlotsEF>(entity =>
        // {
        //     entity.ToTable("SmeltingSlots", tb => tb.HasTrigger("trg_smelting_slots_version"));
        // });

        // modelBuilder.Entity<BoostsEF>(entity =>
        // {
        //     entity.ToTable("Boosts", tb => tb.HasTrigger("trg_boosts_version"));
        // });

        // modelBuilder.Entity<BuildplateEF>(entity =>
        // {
        //     entity.ToTable("PlayerBuildplates", tb => tb.HasTrigger("trg_buildplates_version"));
        // });

        // modelBuilder.Entity<ItemJournalEntryEF>(entity =>
        // {
        //     entity.ToTable("JournalEntries", tb => tb.HasTrigger("trg_journal_entries_version"));
        // });

        // todo: challenges once implemented

        // modelBuilder.Entity<TokenEF>(entity =>
        // {
        //     entity.ToTable("Tokens", tb => tb.HasTrigger("trg_tokens_version"));
        // });

        // profile
        modelBuilder.Entity<ProfileEF>()
            .HasIndex(a => a.Username)
            .IsUnique();

        modelBuilder.Entity<ProfileEF>()
            .HasIndex(a => a.WebPortalAccountId);

        modelBuilder.Entity<ProfileEF>(entity =>
        {
            entity.Property(e => e.SkinImageData).HasMaxLength(16 * 1024);
        });

        modelBuilder.Entity<ProfileEF>()
            .OwnsOne(x => x.Rubies, builder => builder.ToJson());

        modelBuilder.Entity<ProfileEF>()
            .HasOne(a => a.ProfileVersions)
            .WithOne(av => av.ProfileRef)
            .HasForeignKey<ProfileVersions>(av => av.Id)
            .OnDelete(DeleteBehavior.Cascade)
            .IsRequired();

        modelBuilder.Entity<ProfileEF>()
            .HasMany(a => a.ActivityLogs)
            .WithOne(b => b.Profile)
            .HasForeignKey(b => b.ProfileId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ProfileEF>()
            .HasOne(a => a.Boosts)
            .WithOne(b => b.Profile)
            .HasForeignKey<BoostsEF>(a => a.Id)
            .OnDelete(DeleteBehavior.Cascade)
            .IsRequired();

        modelBuilder.Entity<ProfileEF>()
            .HasMany(a => a.Buildplates)
            .WithOne(b => b.Profile)
            .HasForeignKey(b => b.ProfileId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ProfileEF>()
            .HasOne(a => a.Hotbar)
            .WithOne(h => h.Profile)
            .HasForeignKey<HotbarEF>(h => h.Id)
            .OnDelete(DeleteBehavior.Cascade)
            .IsRequired();

        modelBuilder.Entity<ProfileEF>()
            .HasMany(a => a.StackableItems)
            .WithOne(b => b.Profile)
            .HasForeignKey(b => b.ProfileId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ProfileEF>()
            .HasMany(a => a.NonStackableItems)
            .WithOne(b => b.Profile)
            .HasForeignKey(b => b.ProfileId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ProfileEF>()
            .HasMany(a => a.JournalEntries)
            .WithOne(b => b.Profile)
            .HasForeignKey(b => b.ProfileId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ProfileEF>()
            .HasMany(a => a.RedeemedTappables)
            .WithOne(b => b.Profile)
            .HasForeignKey(b => b.ProfileId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ProfileEF>()
            .HasMany(a => a.Tokens)
            .WithOne(b => b.Profile)
            .HasForeignKey(b => b.ProfileId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ProfileEF>()
            .HasOne(a => a.CraftingSlots)
            .WithOne(c => c.Profile)
            .HasForeignKey<CraftingSlotsEF>(c => c.Id)
            .OnDelete(DeleteBehavior.Cascade)
            .IsRequired();

        modelBuilder.Entity<ProfileEF>()
            .HasOne(a => a.SmeltingSlots)
            .WithOne(s => s.Profile)
            .HasForeignKey<SmeltingSlotsEF>(s => s.Id)
            .OnDelete(DeleteBehavior.Cascade)
            .IsRequired();

        modelBuilder.Entity<ProfileEF>()
            .HasMany(a => a.SharedBuildplates)
            .WithOne(s => s.Profile)
            .HasForeignKey(s => s.ProfileId)
            .OnDelete(DeleteBehavior.Cascade);

        // profile versions
        modelBuilder.Entity<ProfileVersions>()
            .ToTable("ProfileVersions");

        // activity log
        modelBuilder.Entity<ActivityLogEntryEF>()
            .HasDiscriminator<string>("entity_type")
            .HasValue<LevelUpEntryEF>("level_up")
            .HasValue<TappableEntryEF>("tappable_collected")
            .HasValue<JournalItemUnlockedEntryEF>("journal_item_unlocked")
            .HasValue<CraftingCompletedEntryEF>("crafting_completed")
            .HasValue<SmeltingCompletedEntryEF>("smelting_completed")
            .HasValue<BoostActivatedEntryEF>("boost_activated");

        modelBuilder.Entity<ActivityLogEntryEF>()
            .HasKey(e => new { e.ProfileId, e.EntryId, });

        modelBuilder.Entity<ActivityLogEntryEF>()
            .Property(e => e.EntryId)
            .ValueGeneratedOnAdd();

        modelBuilder.Entity<JournalItemUnlockedEntryEF>()
            .Property(e => e.ItemId)
            .HasColumnName("ItemId");

        modelBuilder.Entity<BoostActivatedEntryEF>()
            .Property(e => e.ItemId)
            .HasColumnName("ItemId");

        modelBuilder.Entity<RewardedActivityLogEntryEF>()
            .OwnsOne(e => e.Rewards, navigationBuilder =>
            {
                navigationBuilder.ToJson();

                navigationBuilder.Property(r => r.Items)
                    .HasConversion(
                        v => JsonSerializer.Serialize(v, DbJsonContext.Default.DictionaryGuidInt32),
                        v => JsonSerializer.Deserialize(v, DbJsonContext.Default.DictionaryGuidInt32) ?? new Dictionary<Guid, int>()
                    )
                    .Metadata.SetValueComparer(new DictionaryGuidIntValueComparer());
            });

        // boosts
        modelBuilder.Entity<BoostsEF>()
            .Property(x => x.ActiveBoosts)
            .HasConversion(
                v => JsonSerializer.Serialize(v, DbJsonContext.Default.ActiveBoostArray),
                v => JsonSerializer.Deserialize(v, DbJsonContext.Default.ActiveBoostArray)
                    ?? new BoostsEF.ActiveBoost?[5]
            )
            .Metadata.SetValueComparer(new ArrayValueComparer<BoostsEF.ActiveBoost>(BoostsEF.ActiveBoost.Comparer.Instance));

        // hotbar
        modelBuilder.Entity<HotbarEF>()
            .Property(x => x.Items)
            .HasConversion(
                v => JsonSerializer.Serialize(v, DbJsonContext.Default.ItemArray),
                v => JsonSerializer.Deserialize<HotbarEF.Item?[]>(v, DbJsonContext.Default.ItemArray) ?? new HotbarEF.Item?[7]
            )
            .Metadata.SetValueComparer(new ArrayValueComparer<HotbarEF.Item>(HotbarEF.Item.Comparer.Instance));

        // inventory
        modelBuilder.Entity<StackableItemEF>()
            .HasKey(x => new { x.ProfileId, x.ItemId, });

        modelBuilder.Entity<NonStackableItemInstanceEF>()
            .HasKey(x => new { x.ProfileId, x.ItemId, x.InstanceId, });

        // journal
        modelBuilder.Entity<ItemJournalEntryEF>()
            .HasKey(x => new { x.ProfileId, x.ItemId, });

        // redeemed tappables
        modelBuilder.Entity<RedeemedTappableEF>()
            .HasKey(x => new { x.ProfileId, x.TappableId, });

        // tokens
        modelBuilder.Entity<TokenEF>()
            .HasDiscriminator<string>("token_type")
            .HasValue<LevelUpTokenEF>("level_up")
            .HasValue<JournalItemUnlockedTokenEF>("journal_item_unlocked")
            .HasValue<DailyLoginTokenEF>("daily_login");

        modelBuilder.Entity<TokenEF>()
            .HasKey(e => new { e.ProfileId, e.TokenId, });

        modelBuilder.Entity<TokenEF>()
            .Property(e => e.TokenId)
            .ValueGeneratedOnAdd();

        modelBuilder.Entity<RewardedTokenEF>()
            .OwnsOne(e => e.Rewards, navigationBuilder =>
            {
                navigationBuilder.ToJson();

                navigationBuilder.Property(r => r.Items)
                    .HasConversion(
                        v => JsonSerializer.Serialize(v, DbJsonContext.Default.DictionaryGuidInt32),
                        v => JsonSerializer.Deserialize(v, DbJsonContext.Default.DictionaryGuidInt32) ?? new Dictionary<Guid, int>()
                    )
                    .Metadata.SetValueComparer(new DictionaryGuidIntValueComparer());
            });

        modelBuilder.Entity<DailyLoginTokenEF>()
            .HasIndex(e => e.Date);

        // crafting slots
        modelBuilder.Ignore<CraftingSlotEF.ActiveCraftingJob>();

        modelBuilder.Entity<CraftingSlotsEF>()
            .Property(x => x.Slots)
            .HasConversion(
                v => JsonSerializer.Serialize(v, DbJsonContext.Default.CraftingSlotEFArray),
                v => JsonSerializer.Deserialize(v, DbJsonContext.Default.CraftingSlotEFArray) ?? new CraftingSlotEF[] { new CraftingSlotEF(), new CraftingSlotEF(), new CraftingSlotEF(), }
            )
            .Metadata.SetValueComparer(new ArrayValueComparer<CraftingSlotEF>(CraftingSlotEF.Comparer.Instance));

        // smelting slots
        modelBuilder.Ignore<SmeltingSlotEF.ActiveSmeltingJob>();
        modelBuilder.Ignore<SmeltingSlotEF.BurningR>();
        modelBuilder.Ignore<SmeltingSlotEF.Fuel>();

        modelBuilder.Entity<SmeltingSlotsEF>()
            .Property(x => x.Slots)
            .HasConversion(
                v => JsonSerializer.Serialize(v, DbJsonContext.Default.SmeltingSlotEFArray),
                v => JsonSerializer.Deserialize(v, DbJsonContext.Default.SmeltingSlotEFArray) ?? new SmeltingSlotEF[] { new SmeltingSlotEF(), new SmeltingSlotEF(), new SmeltingSlotEF(), }
            )
            .Metadata.SetValueComparer(new ArrayValueComparer<SmeltingSlotEF>(SmeltingSlotEF.Comparer.Instance));

        // shared buildplates
        modelBuilder.Entity<SharedBuildplateEF>()
            .Property(x => x.Hotbar)
            .HasConversion(
                v => JsonSerializer.Serialize(v, DbJsonContext.Default.HotbarItemArray),
                v => JsonSerializer.Deserialize<SharedBuildplateEF.HotbarItem?[]>(v, DbJsonContext.Default.HotbarItemArray) ?? new SharedBuildplateEF.HotbarItem?[7]
            )
            .Metadata.SetValueComparer(new ArrayValueComparer<SharedBuildplateEF.HotbarItem>(SharedBuildplateEF.HotbarItem.Comparer.Instance));
#pragma warning restore CS8622 // Nullability of reference types in type of parameter doesn't match the target delegate (possibly because of nullability attributes).
#pragma warning restore CS8621 // Nullability of reference types in return type doesn't match the target delegate (possibly because of nullability attributes).
#pragma warning restore IDE0058 // Expression value is never used
#pragma warning restore CS8634 // The type cannot be used as type parameter in the generic type or method. Nullability of type argument doesn't match 'class' constraint.
    }

    [Obsolete("Make sure to only call from the DeleteAll endpoint", false)]
    public async Task DeleteAllAsync(CancellationToken cancellationToken = default)
    {
        var ctx = this;  // todo: bug - needed for compiled queries - https://github.com/dotnet/efcore/issues/35887
        var cancellationTokenL = cancellationToken;

        await ctx.Profiles.ExecuteDeleteAsync(cancellationTokenL);

        await ctx.EncounterBuildplates.ExecuteDeleteAsync(cancellationTokenL);
        await ctx.TemplateBuildplates.ExecuteDeleteAsync(cancellationTokenL);
        await ctx.Tiles.ExecuteDeleteAsync(cancellationTokenL);
    }

    public async Task EnsureAccountExists(Guid id)
    {
        var ctx = this;  // todo: bug - needed for compiled queries - https://github.com/dotnet/efcore/issues/35887
        var idL = id;

        if (await ctx.Profiles.AnyAsync(account => account.Id == idL))
        {
            return;
        }

        await InitAccountAndAddToDb(id, null);
    }

    public async Task<ProfileEF> GetOrCreateAccount(Guid id, long? webPortalAccountId)
    {
        var ctx = this;  // todo: bug - needed for compiled queries - https://github.com/dotnet/efcore/issues/35887
        var idL = id;

        var account = await ctx.Profiles.FirstOrDefaultAsync(account => account.Id == idL);

        if (account is not null)
        {
            return account;
        }

        return await InitAccountAndAddToDb(id, webPortalAccountId);
    }

    private async Task<ProfileEF> InitAccountAndAddToDb(Guid id, long? webPortalAccountId)
    {
        var account = new ProfileEF()
        {
            Id = id,
            WebPortalAccountId = webPortalAccountId,
            CreatedDate = DateTimeOffset.UtcNow,
            Username = null,
            ProfilePictureUrl = null,
        };

        account.ProfileVersions = new ProfileVersions() { Id = id, ProfileRef = account, };
        account.Boosts = new BoostsEF() { Id = id, Profile = account, };
        account.Hotbar = new HotbarEF() { Id = id, Profile = account, };
        account.CraftingSlots = new CraftingSlotsEF() { Id = id, Profile = account, };
        account.SmeltingSlots = new SmeltingSlotsEF() { Id = id, Profile = account, };

        var ctx = this;  // todo: bug - needed for compiled queries - https://github.com/dotnet/efcore/issues/35887

        ctx.Profiles.Add(account);

        await SaveChangesAsync();

        return account;
    }
}
