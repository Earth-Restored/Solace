using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Solace.Common;
using Solace.DB.Models;
using Solace.DB.Models.Common;
using Solace.DB.Models.Global;
using Solace.DB.Models.Player;
using Solace.DB.Models.Player.Workshop;
using Solace.DB.Utils;

namespace Solace.DB;

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

    public DbSet<ProfileEF> Profiles { get; set; }

    public DbSet<ActivityLogEF> ActivityLogs { get; set; }

    public DbSet<BoostsEF> Boosts { get; set; }

    public DbSet<BuildplateEF> PlayerBuildplates { get; set; }

    public DbSet<HotbarEF> Hotbars { get; set; }

    public DbSet<InventoryEF> Inventories { get; set; }

    public DbSet<JournalEF> Journals { get; set; }

    public DbSet<RedeemedTappablesEF> RedeemedTappables { get; set; }

    public DbSet<TokensEF> Tokens { get; set; }

    public DbSet<CraftingSlotsEF> CraftingSlots { get; set; }

    public DbSet<SmeltingSlotsEF> SmeltingSlots { get; set; }

    public DbSet<EncounterBuildplateEF> EncounterBuildplates { get; set; }

    public DbSet<SharedBuildplateEF> SharedBuildplates { get; set; }

    public DbSet<TemplateBuildplateEF> TemplateBuildplates { get; set; }

    public DbSet<Tile> Tiles { get; set; }

    public DbSet<Secret> Secrets { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (typeof(IVersionedEntity).IsAssignableFrom(entityType.ClrType))
            {
                modelBuilder.Entity(entityType.ClrType)
                    .Property(nameof(IVersionedEntity.Version))
                    .IsConcurrencyToken();
            }
        }

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
            .HasOne(a => a.Profile)
            .WithOne(p => p.Account)
            .HasForeignKey<ProfileEF>(p => p.Id)
            .OnDelete(DeleteBehavior.Cascade)
            .IsRequired();

        modelBuilder.Entity<Account>()
            .HasOne(a => a.ActivityLog)
            .WithOne(a => a.Account)
            .HasForeignKey<ActivityLogEF>(a => a.Id)
            .OnDelete(DeleteBehavior.Cascade)
            .IsRequired();

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
            .HasOne(a => a.Inventory)
            .WithOne(i => i.Account)
            .HasForeignKey<InventoryEF>(i => i.Id)
            .OnDelete(DeleteBehavior.Cascade)
            .IsRequired();

        modelBuilder.Entity<Account>()
            .HasOne(a => a.Journal)
            .WithOne(j => j.Account)
            .HasForeignKey<JournalEF>(j => j.Id)
            .OnDelete(DeleteBehavior.Cascade)
            .IsRequired();

        modelBuilder.Entity<Account>()
            .HasOne(a => a.RedeemedTappables)
            .WithOne(r => r.Account)
            .HasForeignKey<RedeemedTappablesEF>(r => r.Id)
            .OnDelete(DeleteBehavior.Cascade)
            .IsRequired();

        modelBuilder.Entity<Account>()
            .HasOne(a => a.Tokens)
            .WithOne(t => t.Account)
            .HasForeignKey<TokensEF>(t => t.Id)
            .OnDelete(DeleteBehavior.Cascade)
            .IsRequired();

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
        modelBuilder.Entity<ActivityLogEF>()
            .Property(x => x.Entries)
            .HasConversion<ActivityLogValueConverter>()
            .Metadata.SetValueComparer(new ActivityLogListValueComparer());

        // boosts
        modelBuilder.Entity<BoostsEF>()
            .Property(x => x.ActiveBoosts)
            .HasConversion<ActiveBoostValueConverter>()
            .Metadata.SetValueComparer(new ActiveBoostArrayValueComparer());

        // hotbar
        modelBuilder.Entity<HotbarEF>()
            .Property(x => x.Items)
            .HasConversion<HotbarValueConverter>()
            .Metadata.SetValueComparer(new HotbarArrayValueComparer());

        // inventory
        modelBuilder.Ignore<NonStackableItemInstance>();

        modelBuilder.Entity<InventoryEF>()
            .Property(x => x.StackableItemsData)
            .HasConversion<StackableItemValueConverter>()
            .Metadata.SetValueComparer(new StackableItemDictionaryValueComparer());

        modelBuilder.Entity<InventoryEF>()
            .Property(x => x.NonStackableItemsData)
            .HasConversion<NonStackableItemValueConverter>()
            .Metadata.SetValueComparer(new NonStackableItemDictionaryValueComparer());

        // journal
        modelBuilder.Entity<JournalEF>()
            .Property(x => x.Items)
            .HasConversion<JournalValueConverter>()
            .Metadata.SetValueComparer(new JournalDictionaryValueComparer());

        // redeemed tappables
        modelBuilder.Entity<RedeemedTappablesEF>()
            .OwnsOne(x => x.Tappables, builder => builder.ToJson());

        // tokens
        modelBuilder.Entity<TokensEF>()
            .Property(x => x.Tokens)
            .HasConversion<TokenValueConverter>()
            .Metadata.SetValueComparer(new TokenDictionaryValueComparer());

        // crafting slots
        modelBuilder.Ignore<CraftingSlotEF.ActiveCraftingJob>();

        modelBuilder.Entity<CraftingSlotsEF>()
            .Property(x => x.Slots)
            .HasConversion<CraftingSlotValueConverter>()
            .Metadata.SetValueComparer(new CraftingSlotArrayValueComparer());

        // smelting slots
        modelBuilder.Ignore<SmeltingSlot.ActiveSmeltingJob>();
        modelBuilder.Ignore<SmeltingSlot.BurningR>();
        modelBuilder.Ignore<SmeltingSlot.Fuel>();

        modelBuilder.Entity<SmeltingSlotsEF>()
            .Property(x => x.Slots)
            .HasConversion<SmeltingSlotValueConverter>()
            .Metadata.SetValueComparer(new SmeltingSlotArrayValueComparer());

        // shared buildplates
        modelBuilder.Entity<SharedBuildplateEF>()
            .Property(x => x.Hotbar)
            .HasConversion<SBHotbarValueConverter>()
            .Metadata.SetValueComparer(new SBHotbarArrayValueComparer());
    }

    public async Task ClearAsync(CancellationToken cancellationToken = default)
    {
        await Accounts.ExecuteDeleteAsync(cancellationToken);

        await EncounterBuildplates.ExecuteDeleteAsync(cancellationToken);
        await TemplateBuildplates.ExecuteDeleteAsync(cancellationToken);
        await Tiles.ExecuteDeleteAsync(cancellationToken);
    }

    public async Task EnsureAccountExists(Guid id)
    {
        if (await Accounts.AnyAsync(account => account.Id == id))
        {
            return;
        }

        await InitAccountAndAddToDb(id);
    }

    public async Task<Account> GetOrCreateAccount(Guid id, Func<IQueryable<Account>, IQueryable<Account>> queryFunc)
    {
        var account = await queryFunc(Accounts)
            .FirstOrDefaultAsync(account => account.Id == id);

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
            CreatedDate = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            Username = null,
            ProfilePictureUrl = null,
            FirstName = null,
            LastName = null,
            PasswordSalt = new byte[16],
            PasswordHash = new byte[64],
        };

        account.Profile = new ProfileEF() { Id = id, Account = account, };
        account.ActivityLog = new ActivityLogEF() { Id = id, Account = account, };
        account.Boosts = new BoostsEF() { Id = id, Account = account, };
        account.Hotbar = new HotbarEF() { Id = id, Account = account, };
        account.Inventory = new InventoryEF() { Id = id, Account = account, };
        account.Journal = new JournalEF() { Id = id, Account = account, };
        account.RedeemedTappables = new RedeemedTappablesEF() { Id = id, Account = account, };
        account.Tokens = new TokensEF() { Id = id, Account = account, };
        account.CraftingSlots = new CraftingSlotsEF() { Id = id, Account = account, };
        account.SmeltingSlots = new SmeltingSlotsEF() { Id = id, Account = account, };

        Accounts.Add(account);

        await SaveChangesAsync();

        return account;
    }

    public sealed class Results
    {
        [SetsRequiredMembers]
        public Results(EarthDbContext earthDb)
        {
            EarthDb = earthDb;
        }

        public required EarthDbContext EarthDb { get; init; }

        [DisallowNull]
        public int? Profile { get; set; }

        [DisallowNull]
        public int? Inventory { get; set; }

        [DisallowNull]
        public int? Crafting { get; set; }

        [DisallowNull]
        public int? Smelting { get; set; }

        [DisallowNull]
        public int? Boosts { get; set; }

        [DisallowNull]
        public int? Buildplates { get; set; }

        [DisallowNull]
        public int? Journal { get; set; }

        [DisallowNull]
        public int? Challenges { get; set; }

        [DisallowNull]
        public int? Tokens { get; set; }
    }
}
