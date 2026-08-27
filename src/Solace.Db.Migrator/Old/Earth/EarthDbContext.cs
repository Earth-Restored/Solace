using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Solace.Common;
using Solace.Db.Migrator.Old.Earth.Utils;
using Solace.Db.Migrator.Old.Earth.Models.Global;
using Solace.Db.Migrator.Old.Earth.Models.Player.Workshop;
using Solace.Db.Migrator.Old.Earth.Models.Player;
using Solace.Db.Migrator.Old.Earth.Models;
using Solace.Db.Migrator.Old.Earth.Models.Common;

namespace Solace.Db.Migrator.Old.Earth;

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

    public DbSet<Secret> Secrets { get; set; }

    public static EarthDbContext CreateFromPath(string path)
        => CreateFromConnection("Data Source=" + Path.GetFullPath(path));

    public static EarthDbContext CreateFromConnection(string connectionString)
    {
        var optionsBuilder = new DbContextOptionsBuilder<EarthDbContext>();
        ConfigureBuilder(optionsBuilder, connectionString);

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
            .HasIndex(a => a.Username)
            .IsUnique();

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
                    ?? new Dictionary<string, int>(StringComparer.Ordinal)
            )
            .Metadata.SetValueComparer(new DictionaryStringIntValueComparer());

        modelBuilder.Entity<InventoryEF>()
            .Property(x => x.NonStackableItemsData)
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions)null!),
                v => JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, NonStackableItemInstance>>>(v, (JsonSerializerOptions)null!)
                    ?? new Dictionary<string, Dictionary<string, NonStackableItemInstance>>(StringComparer.Ordinal)
            )
            .Metadata.SetValueComparer(new NestedDictionaryValueComparer());

        // journal
        modelBuilder.Entity<JournalEF>()
            .Property(x => x.Items)
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions)null!),
                v => JsonSerializer.Deserialize<Dictionary<string, JournalEF.ItemJournalEntry>>(v, (JsonSerializerOptions)null!)
                    ?? new Dictionary<string, JournalEF.ItemJournalEntry>(StringComparer.Ordinal)
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
                    ?? new Dictionary<string, TokensEF.Token>(StringComparer.Ordinal)
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
}

#pragma warning disable MA0048 // File name must match type name
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
            d => new Dictionary<string, TValue>(d.Select(item => new KeyValuePair<string, TValue>(item.Key, item.Value.DeepCopy())), StringComparer.Ordinal))
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
            hash.Add(kvp.Key, StringComparer.Ordinal);
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
            d => new Dictionary<string, int>(d.Select(item => new KeyValuePair<string, int>(item.Key, item.Value)), StringComparer.Ordinal))
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
            hash.Add(kvp.Key, StringComparer.Ordinal);
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
            d => d.ToDictionary(x => x.Key, x => new Dictionary<string, NonStackableItemInstance>(x.Value.Select(item => new KeyValuePair<string, NonStackableItemInstance>(item.Key, item.Value.DeepCopy())), StringComparer.Ordinal)))
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
        foreach (var kvp in d.OrderBy(x => x.Key, StringComparer.Ordinal))
        {
            hash.Add(kvp.Key, StringComparer.Ordinal);
            foreach (var innerKvp in kvp.Value.OrderBy(x => x.Key, StringComparer.Ordinal))
            {
                hash.Add(innerKvp.Key, StringComparer.Ordinal);
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
#pragma warning disable CS8619 // Nullability of reference types in value doesn't match target type.
            a => a != null ? a.Select(item => item == null ? null : item.DeepCopy()).ToArray() : Array.Empty<T>())
#pragma warning restore CS8619 // Nullability of reference types in value doesn't match target type.
    {
    }
}
#pragma warning restore MA0048 // File name must match type name
