using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using BitcoderCZ.Utils;
using Solace.Common;
using Solace.Common.Utils;

namespace Solace.DB.Models.Player;

public sealed class HotbarEF : IEntityWithId<Guid>, IVersionedEntity, IMergeable<HotbarEF>
{
    public Guid Id { get; set; }

    public int Version { get; set; } = 1;

    public Account Account { get; set; } = null!;

    public Item?[] Items { get; set; } = new Item[7];

    public void LimitToInventory(InventoryEF inventory)
    {
        ThrowHelper.ThrowIfNull(inventory);

        Dictionary<Guid, int> usedStackableItemCounts = [];
        Dictionary<Guid, HashSet<Guid>> usedNonStackableItemInstances = [];

        for (int index = 0; index < Items.Length; index++)
        {
            Item? item = Items[index];
            if (item is null)
            {
                continue;
            }

            if (item.InstanceId is not null)
            {
                if (inventory.GetItemInstance(item.Uuid, item.InstanceId.Value) is not null)
                {
                    var usedItemInstances = usedNonStackableItemInstances.ComputeIfAbsent(item.Uuid, uuid => [])!;

                    if (!usedItemInstances.Add(item.InstanceId.Value))
                    {
                        item = null;
                    }
                }
                else
                {
                    item = null;
                }
            }
            else
            {
                int inventoryCount = inventory.GetItemCount(item.Uuid);

                int usedCount = usedStackableItemCounts.GetValueOrDefault(item.Uuid);
                if (inventoryCount - usedCount > 0)
                {
                    if (inventoryCount - usedCount < item.Count)
                    {
                        item = new Item(item.Uuid, inventoryCount - usedCount, null);
                    }

                    usedCount += item.Count;
                    usedStackableItemCounts[item.Uuid] = usedCount;
                }
                else
                {
                    item = null;
                }
            }

            Items[index] = item;
        }
    }

    public async Task MergeWith(HotbarEF other, ValueMerger merger)
    {
        merger.CurrentUserId = Id.ToString();
        merger.CurrentUsername = Account?.Username;

        for (var i = 0; i < Items.Length; i++)
        {
            Items[i] = await merger.AutoMerge(Items[i], other.Items[i], $"Hotbar slot {i + 1}", null);
        }
    }

    public sealed record Item(
        Guid Uuid,
        int Count,
        Guid? InstanceId
    ) : ICloneable<Item>
    {
        public Item DeepCopy()
            => new Item(this);

        public sealed class Comparer : IEqualityComparer<Item>
        {
            public static Comparer Instance { get; } = new Comparer();

            private Comparer()
            {
            }

            public bool Equals(Item? x, Item? y)
                => x == y || (x?.Equals(y) ?? false);

            public int GetHashCode([DisallowNull] Item obj)
                => obj.GetHashCode();
        }
    }

    public sealed class Legacy : IEquatable<Legacy>
    {
        public Item?[] Items { get; set; }

        public Legacy()
        {
            Items = new Item[7];
        }

        public bool Equals(Legacy? other)
            => other is not null && Items.SequenceEqual(other.Items);

        public override bool Equals(object? obj)
            => Equals(obj as Legacy);

        public override int GetHashCode()
        {
            var hash = new HashCode();

            foreach (var item in Items)
            {
                hash.Add(item);
            }

            return hash.ToHashCode();
        }

        public sealed record Item(
            Guid Uuid,
            int Count,
            Guid? InstanceId
        );
    }
}

#region Converter
public sealed class HotbarValueConverter : Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter<HotbarEF.Item?[], string>
{
    public HotbarValueConverter() : base(
        v => JsonSerializer.Serialize(v, DbJsonContext.Default.ItemArray),
        v => JsonSerializer.Deserialize(v, DbJsonContext.Default.ItemArray) ?? new HotbarEF.Item?[7])
    {
    }
}

public sealed class HotbarArrayValueComparer : Microsoft.EntityFrameworkCore.ChangeTracking.ValueComparer<HotbarEF.Item?[]>
{
    public HotbarArrayValueComparer() : base(
        (a, b) => CompareArrays(a, b),
        a => GetArrayHashCode(a),
        a => SnapshotArray(a))
    {
    }

    public static bool CompareArrays(HotbarEF.Item?[]? a, HotbarEF.Item?[]? b)
    {
        if (ReferenceEquals(a, b))
        {
            return true;
        }

        if (a is null || b is null)
        {
            return false;
        }

        if (a.Length != b.Length)
        {
            return false;
        }

        for (int i = 0; i < a.Length; i++)
        {
            if (!HotbarEF.Item.Comparer.Instance.Equals(a[i], b[i]))
            {
                return false;
            }
        }

        return true;
    }

    public static int GetArrayHashCode(HotbarEF.Item?[] a)
    {
        var hash = new HashCode();
        foreach (var item in a)
        {
            hash.Add(item is not null ? HotbarEF.Item.Comparer.Instance.GetHashCode(item) : 0);
        }

        return hash.ToHashCode();
    }

    public static HotbarEF.Item?[] SnapshotArray(HotbarEF.Item?[] a)
    {
        var clone = new HotbarEF.Item?[a.Length];
        for (int i = 0; i < a.Length; i++)
        {
            clone[i] = a[i]?.DeepCopy();
        }

        return clone;
    }
}
#endregion
