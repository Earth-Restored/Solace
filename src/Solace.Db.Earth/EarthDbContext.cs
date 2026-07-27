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

    public DbSet<Account> Accounts { get; set; }

    public DbSet<AccountVersions> AccountVersions { get; set; }

    public DbSet<ProfileEF> Profiles { get; set; }

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

        // account
        modelBuilder.Entity<Account>()
            .HasIndex(a => a.Username)
            .IsUnique();

        modelBuilder.Entity<Account>(entity =>
        {
            entity.Property(e => e.PasswordSalt).HasMaxLength(16);
            entity.Property(e => e.PasswordHash).HasMaxLength(64);
            entity.Property(e => e.SkinImageData).HasMaxLength(16 * 1024);
        });

        modelBuilder.Entity<Account>()
            .HasOne(a => a.AccountVersions)
            .WithOne(av => av.Account)
            .HasForeignKey<AccountVersions>(av => av.Id)
            .OnDelete(DeleteBehavior.Cascade)
            .IsRequired();

        modelBuilder.Entity<Account>()
            .HasOne(a => a.Profile)
            .WithOne(p => p.Account)
            .HasForeignKey<ProfileEF>(p => p.Id)
            .OnDelete(DeleteBehavior.Cascade)
            .IsRequired();

        modelBuilder.Entity<Account>()
            .HasMany(a => a.ActivityLogs)
            .WithOne(b => b.Account)
            .HasForeignKey(b => b.AccountId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Account>()
            .HasOne(a => a.Boosts)
            .WithOne(b => b.Account)
            .HasForeignKey<BoostsEF>(a => a.Id)
            .OnDelete(DeleteBehavior.Cascade)
            .IsRequired();

        modelBuilder.Entity<Account>()
            .HasMany(a => a.Buildplates)
            .WithOne(b => b.Account)
            .HasForeignKey(b => b.AccountId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Account>()
            .HasOne(a => a.Hotbar)
            .WithOne(h => h.Account)
            .HasForeignKey<HotbarEF>(h => h.Id)
            .OnDelete(DeleteBehavior.Cascade)
            .IsRequired();

        modelBuilder.Entity<Account>()
            .HasMany(a => a.StackableItems)
            .WithOne(b => b.Account)
            .HasForeignKey(b => b.AccountId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Account>()
            .HasMany(a => a.NonStackableItems)
            .WithOne(b => b.Account)
            .HasForeignKey(b => b.AccountId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Account>()
            .HasMany(a => a.JournalEntries)
            .WithOne(b => b.Account)
            .HasForeignKey(b => b.AccountId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Account>()
            .HasMany(a => a.RedeemedTappables)
            .WithOne(b => b.Account)
            .HasForeignKey(b => b.AccountId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Account>()
            .HasMany(a => a.Tokens)
            .WithOne(b => b.Account)
            .HasForeignKey(b => b.AccountId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Account>()
            .HasOne(a => a.CraftingSlots)
            .WithOne(c => c.Account)
            .HasForeignKey<CraftingSlotsEF>(c => c.Id)
            .OnDelete(DeleteBehavior.Cascade)
            .IsRequired();

        modelBuilder.Entity<Account>()
            .HasOne(a => a.SmeltingSlots)
            .WithOne(s => s.Account)
            .HasForeignKey<SmeltingSlotsEF>(s => s.Id)
            .OnDelete(DeleteBehavior.Cascade)
            .IsRequired();

        modelBuilder.Entity<Account>()
            .HasMany(a => a.SharedBuildplates)
            .WithOne(s => s.Account)
            .HasForeignKey(s => s.AccountId)
            .OnDelete(DeleteBehavior.Cascade);

        // profile
        modelBuilder.Entity<ProfileEF>()
            .OwnsOne(x => x.Rubies, builder => builder.ToJson());

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
            .HasKey(e => new { e.AccountId, e.EntryId, });

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
            .HasKey(x => new { x.AccountId, x.ItemId, });

        modelBuilder.Entity<NonStackableItemInstanceEF>()
            .HasKey(x => new { x.AccountId, x.ItemId, x.InstanceId, });

        // journal
        modelBuilder.Entity<ItemJournalEntryEF>()
            .HasKey(x => new { x.AccountId, x.ItemId, });

        // redeemed tappables
        modelBuilder.Entity<RedeemedTappableEF>()
            .HasKey(x => new { x.AccountId, x.TappableId, });

        // tokens
        modelBuilder.Entity<TokenEF>()
            .HasDiscriminator<string>("token_type")
            .HasValue<LevelUpTokenEF>("level_up")
            .HasValue<JournalItemUnlockedTokenEF>("journal_item_unlocked")
            .HasValue<DailyLoginTokenEF>("daily_login");

        modelBuilder.Entity<TokenEF>()
            .HasKey(e => new { e.AccountId, e.TokenId, });

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

    public async Task ClearAsync(CancellationToken cancellationToken = default)
    {
        var ctx = this;  // todo: bug - needed for compiled queries - https://github.com/dotnet/efcore/issues/35887
        var cancellationTokenL = cancellationToken;

        await ctx.Accounts.ExecuteDeleteAsync(cancellationTokenL);

        await ctx.EncounterBuildplates.ExecuteDeleteAsync(cancellationTokenL);
        await ctx.TemplateBuildplates.ExecuteDeleteAsync(cancellationTokenL);
        await ctx.Tiles.ExecuteDeleteAsync(cancellationTokenL);
    }

    public async Task EnsureAccountExists(Guid id)
    {
        var ctx = this;  // todo: bug - needed for compiled queries - https://github.com/dotnet/efcore/issues/35887
        var idL = id;

        if (await ctx.Accounts.AnyAsync(account => account.Id == idL))
        {
            return;
        }

        await InitAccountAndAddToDb(id);
    }

    public async Task<Account> GetOrCreateAccount(Guid id)
    {
        var ctx = this;  // todo: bug - needed for compiled queries - https://github.com/dotnet/efcore/issues/35887
        var idL = id;

        var account = await ctx.Accounts.FirstOrDefaultAsync(account => account.Id == idL);

        if (account is not null)
        {
            return account;
        }

        return await InitAccountAndAddToDb(id);
    }

    private async Task<Account> InitAccountAndAddToDb(Guid id)
    {
        var account = new Account()
        {
            Id = id,
            CreatedDate = DateTimeOffset.UtcNow,
            Username = null,
            ProfilePictureUrl = null,
            FirstName = null,
            LastName = null,
            PasswordSalt = new byte[16],
            PasswordHash = new byte[64],
        };

        account.AccountVersions = new AccountVersions() { Id = id, Account = account, };
        account.Profile = new ProfileEF() { Id = id, Account = account, };
        account.Boosts = new BoostsEF() { Id = id, Account = account, };
        account.Hotbar = new HotbarEF() { Id = id, Account = account, };
        account.CraftingSlots = new CraftingSlotsEF() { Id = id, Account = account, };
        account.SmeltingSlots = new SmeltingSlotsEF() { Id = id, Account = account, };

        var ctx = this;  // todo: bug - needed for compiled queries - https://github.com/dotnet/efcore/issues/35887

        ctx.Accounts.Add(account);

        await SaveChangesAsync();

        return account;
    }
}
