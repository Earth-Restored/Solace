using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text.Json;
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
    public EarthDbContext(DbContextOptions<EarthDbContext> options)
        : base(options)
    {
    }

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

    public static EarthDbContext Create(string path)
    {
        var optionsBuilder = new DbContextOptionsBuilder<EarthDbContext>();
        ConfigureBuilder(optionsBuilder, "Data Source=" + Path.GetFullPath(path));

        return new EarthDbContext(optionsBuilder.Options);
    }

    public static void ConfigureBuilder(DbContextOptionsBuilder optionsBuilder, string connectionString)
    {
        optionsBuilder.UseSqlite(connectionString);
        optionsBuilder.AddInterceptors(new VersioningInterceptor());
    }

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
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions)null!),
                v => JsonSerializer.Deserialize<List<ActivityLogEF.Entry>>(v, (JsonSerializerOptions)null!)
                    ?? new List<ActivityLogEF.Entry>()
            )
            .Metadata.SetValueComparer(new ListValueComparer<ActivityLogEF.Entry>(ActivityLogEF.Entry.Comparer.Instance));

        // boosts
        modelBuilder.Entity<BoostsEF>()
            .Property(x => x.ActiveBoosts)
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions)null!),
                v => JsonSerializer.Deserialize<BoostsEF.ActiveBoost?[]>(v, (JsonSerializerOptions)null!)
                    ?? new BoostsEF.ActiveBoost?[5]
            )
            .Metadata.SetValueComparer(new ArrayValueComparer<BoostsEF.ActiveBoost>(BoostsEF.ActiveBoost.Comparer.Instance));

        // hotbar
        modelBuilder.Entity<HotbarEF>()
            .Property(x => x.Items)
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions)null!),
                v => JsonSerializer.Deserialize<HotbarEF.Item?[]>(v, (JsonSerializerOptions)null!)
                    ?? new HotbarEF.Item?[7]
            )
            .Metadata.SetValueComparer(new ArrayValueComparer<HotbarEF.Item>(HotbarEF.Item.Comparer.Instance));

        // inventory
        modelBuilder.Ignore<NonStackableItemInstance>();

        modelBuilder.Entity<InventoryEF>()
            .Property(x => x.StackableItemsData)
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions)null!),
                v => JsonSerializer.Deserialize<Dictionary<string, int>>(v, (JsonSerializerOptions)null!)
                    ?? new Dictionary<string, int>()
            )
            .Metadata.SetValueComparer(new DictionaryStringIntValueComparer());

        modelBuilder.Entity<InventoryEF>()
            .Property(x => x.NonStackableItemsData)
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions)null!),
                v => JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, NonStackableItemInstance>>>(v, (JsonSerializerOptions)null!)
                    ?? new Dictionary<string, Dictionary<string, NonStackableItemInstance>>()
            )
            .Metadata.SetValueComparer(new NestedDictionaryValueComparer());

        // journal
        modelBuilder.Entity<JournalEF>()
            .Property(x => x.Items)
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions)null!),
                v => JsonSerializer.Deserialize<Dictionary<string, JournalEF.ItemJournalEntry>>(v, (JsonSerializerOptions)null!)
                    ?? new Dictionary<string, JournalEF.ItemJournalEntry>()
            )
            .Metadata.SetValueComparer(new DictionaryStringTValueComparer<JournalEF.ItemJournalEntry>(JournalEF.ItemJournalEntry.Comparer.Instance));

        // redeemed tappables
        modelBuilder.Entity<RedeemedTappablesEF>()
            .OwnsOne(x => x.Tappables, builder => builder.ToJson());

        // tokens
        modelBuilder.Entity<TokensEF>()
            .Property(x => x.Tokens)
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions)null!),
                v => JsonSerializer.Deserialize<Dictionary<string, TokensEF.Token>>(v, (JsonSerializerOptions)null!)
                    ?? new Dictionary<string, TokensEF.Token>()
            )
            .Metadata.SetValueComparer(new DictionaryStringTValueComparer<TokensEF.Token>(TokensEF.Token.Comparer.Instance));

        // crafting slots
        modelBuilder.Ignore<CraftingSlotEF.ActiveJobR>();

        modelBuilder.Entity<CraftingSlotsEF>()
            .Property(x => x.Slots)
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions)null!),
                v => JsonSerializer.Deserialize<CraftingSlotEF[]>(v, (JsonSerializerOptions)null!)
                    ?? new CraftingSlotEF[3]
            )
            .Metadata.SetValueComparer(new ArrayValueComparer<CraftingSlotEF>(CraftingSlotEF.Comparer.Instance));

        // smelting slots
        modelBuilder.Ignore<SmeltingSlot.ActiveJobR>();
        modelBuilder.Ignore<SmeltingSlot.BurningR>();
        modelBuilder.Ignore<SmeltingSlot.Fuel>();

        modelBuilder.Entity<SmeltingSlotsEF>()
            .Property(x => x.Slots)
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions)null!),
                v => JsonSerializer.Deserialize<SmeltingSlot[]>(v, (JsonSerializerOptions)null!)
                    ?? new SmeltingSlot[3]
            )
            .Metadata.SetValueComparer(new ArrayValueComparer<SmeltingSlot>(SmeltingSlot.Comparer.Instance));

        // shared buildplates
        modelBuilder.Entity<SharedBuildplateEF>()
            .Property(x => x.Hotbar)
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions)null!),
                v => JsonSerializer.Deserialize<SharedBuildplateEF.HotbarItem?[]>(v, (JsonSerializerOptions)null!)
                    ?? new SharedBuildplateEF.HotbarItem?[7]
            )
            .Metadata.SetValueComparer(new ArrayValueComparer<SharedBuildplateEF.HotbarItem>(SharedBuildplateEF.HotbarItem.Comparer.Instance));
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
            Username = "[null]",
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

public sealed class ListValueComparer<T> : ValueComparer<List<T>>
    where T : IEquatable<T>, ICloneable<T>
{
    public ListValueComparer(IEqualityComparer<T> equalityComparer)
        : base(
            (c1, c2) => c1 == c2 || (c1 != null && c2 != null && c1.SequenceEqual(c2, equalityComparer)),
            c => c != null ? c.Aggregate(0, (h, v) => HashCode.Combine(h, equalityComparer.GetHashCode(v))) : 0,
            c => new List<T>(c.Select(item => item.DeepCopy())))
    {
    }
}

public sealed class DictionaryStringTValueComparer<TValue> : ValueComparer<Dictionary<string, TValue>>
    where TValue : ICloneable<TValue>
{
    public DictionaryStringTValueComparer(IEqualityComparer<TValue> equalityComparer)
        : base(
            (d1, d2) => DictionariesEqual(d1, d2, equalityComparer),
            d => ComputeHashCode(d, equalityComparer),
            d => new Dictionary<string, TValue>(d.Select(item => new KeyValuePair<string, TValue>(item.Key, item.Value.DeepCopy()))))
    {
    }

    private static bool DictionariesEqual(Dictionary<string, TValue>? d1, Dictionary<string, TValue>? d2, IEqualityComparer<TValue> equalityComparer)
    {
        if (d1 == d2)
        {
            return true;
        }

        if (d1 == null || d2 == null)
        {
            return false;
        }

        if (d1.Count != d2.Count)
        {
            return false;
        }

        foreach (var kvp in d1)
        {
            if (!d2.TryGetValue(kvp.Key, out var value2))
            {
                return false;
            }

            if (!equalityComparer.Equals(kvp.Value, value2))
            {
                return false;
            }
        }

        return true;
    }

    private static int ComputeHashCode(Dictionary<string, TValue>? d, IEqualityComparer<TValue> equalityComparer)
    {
        if (d == null)
        {
            return 0;
        }

        var hash = new HashCode();
        foreach (var kvp in d.OrderBy(x => x.Key, StringComparer.Ordinal))
        {
            hash.Add(kvp.Key);
            hash.Add(kvp.Value, equalityComparer);
        }

        return hash.ToHashCode();
    }
}

public sealed class DictionaryStringIntValueComparer : ValueComparer<Dictionary<string, int>>
{
    public DictionaryStringIntValueComparer()
        : base(
            (d1, d2) => DictionariesEqual(d1, d2),
            d => ComputeHashCode(d),
            d => new Dictionary<string, int>(d.Select(item => new KeyValuePair<string, int>(item.Key, item.Value))))
    {
    }

    private static bool DictionariesEqual(Dictionary<string, int>? d1, Dictionary<string, int>? d2)
    {
        if (d1 == d2)
        {
            return true;
        }

        if (d1 == null || d2 == null)
        {
            return false;
        }

        if (d1.Count != d2.Count)
        {
            return false;
        }

        foreach (var kvp in d1)
        {
            if (!d2.TryGetValue(kvp.Key, out var value2))
            {
                return false;
            }

            if (kvp.Value != value2)
            {
                return false;
            }
        }

        return true;
    }

    private static int ComputeHashCode(Dictionary<string, int>? d)
    {
        if (d == null)
        {
            return 0;
        }

        var hash = new HashCode();
        foreach (var kvp in d.OrderBy(x => x.Key, StringComparer.Ordinal))
        {
            hash.Add(kvp.Key);
            hash.Add(kvp.Value);
        }

        return hash.ToHashCode();
    }
}

public sealed class NestedDictionaryValueComparer : ValueComparer<Dictionary<string, Dictionary<string, NonStackableItemInstance>>>
{
    public NestedDictionaryValueComparer()
        : base(
            (d1, d2) => OuterDictionariesEqual(d1, d2),
            d => ComputeOuterHashCode(d),
            d => d.ToDictionary(x => x.Key, x => new Dictionary<string, NonStackableItemInstance>(x.Value.Select(item => new KeyValuePair<string, NonStackableItemInstance>(item.Key, item.Value.DeepCopy())))))
    {
    }

    private static bool OuterDictionariesEqual(Dictionary<string, Dictionary<string, NonStackableItemInstance>>? d1, Dictionary<string, Dictionary<string, NonStackableItemInstance>>? d2)
    {
        if (d1 == d2)
        {
            return true;
        }

        if (d1 == null || d2 == null)
        {
            return false;
        }

        if (d1.Count != d2.Count)
        {
            return false;
        }

        foreach (var kvp in d1)
        {
            if (!d2.TryGetValue(kvp.Key, out var innerDict2))
            {
                return false;
            }

            if (!InnerDictionariesEqual(kvp.Value, innerDict2))
            {
                return false;
            }
        }

        return true;
    }

    private static bool InnerDictionariesEqual(Dictionary<string, NonStackableItemInstance>? d1, Dictionary<string, NonStackableItemInstance>? d2)
    {
        if (d1 == d2)
        {
            return true;
        }

        if (d1 == null || d2 == null)
        {
            return false;
        }

        if (d1.Count != d2.Count)
        {
            return false;
        }

        foreach (var kvp in d1)
        {
            if (!d2.TryGetValue(kvp.Key, out var item2))
            {
                return false;
            }

            if (!kvp.Value.Equals(item2))
            {
                return false;
            }
        }

        return true;
    }

    private static int ComputeOuterHashCode(Dictionary<string, Dictionary<string, NonStackableItemInstance>>? d)
    {
        if (d == null)
        {
            return 0;
        }

        var hash = new HashCode();
        foreach (var kvp in d.OrderBy(x => x.Key))
        {
            hash.Add(kvp.Key);
            foreach (var innerKvp in kvp.Value.OrderBy(x => x.Key))
            {
                hash.Add(innerKvp.Key);
                hash.Add(innerKvp.Value);
            }
        }

        return hash.ToHashCode();
    }
}

public sealed class ArrayValueComparer<T> : ValueComparer<T[]>
    where T : class, ICloneable<T>
{
    public ArrayValueComparer(IEqualityComparer<T> equalityComparer)
        : base(
            (a1, a2) => a1 == a2 || (a1 != null && a2 != null && a1.SequenceEqual(a2, equalityComparer)),
            a => a != null ? a.Aggregate(0, (h, v) => HashCode.Combine(h, equalityComparer.GetHashCode(v))) : 0,
            a => a != null ? a.Select(item => item == null ? null : item.DeepCopy()).ToArray() : Array.Empty<T>())
    {
    }
}