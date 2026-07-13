using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;
using System.Text.Json.Serialization;
using Solace.Common.Utils;
using Solace.DB.Models.Common;

using StackableItemData = System.Collections.Generic.Dictionary<System.Guid, int>;
using NonStackableItemData = System.Collections.Generic.Dictionary<System.Guid, System.Collections.Generic.Dictionary<System.Guid, Solace.DB.Models.Common.NonStackableItemInstance>>;

namespace Solace.DB.Models.Player;

public sealed class InventoryEF : IEntityWithId<Guid>, IVersionedEntity, IMergeable<InventoryEF>
{
    public Guid Id { get; set; }

    public int Version { get; set; } = 1;

    public Account Account { get; set; } = null!;

    // id to count
    public StackableItemData StackableItemsData { get; set; } = [];

    // id to (instanceId to instance)
    public NonStackableItemData NonStackableItemsData { get; set; } = [];

    [JsonIgnore, NotMapped]
    public IEnumerable<StackableItem> StackableItems => StackableItemsData.Select(item => new StackableItem(item.Key, item.Value));

    [JsonIgnore, NotMapped]
    public IEnumerable<NonStackableItem> NonStackableItems => NonStackableItemsData.Select(item => new NonStackableItem(item.Key, [.. item.Value.Values]));

    public sealed record StackableItem(
        Guid Id,
        int Count
    );

    public sealed record NonStackableItem(
        Guid Id,
        NonStackableItemInstance[] Instances
    );

    public int GetItemCount(Guid id)
    {
        if (StackableItemsData.TryGetValue(id, out var count))
        {
            return count;
        }

        var instances = NonStackableItemsData!.GetValueOrDefault(id);

        return instances is not null
            ? instances.Count
            : 0;
    }

    public NonStackableItemInstance[] GetItemInstances(Guid id)
    {
        var instances = NonStackableItemsData!.GetValueOrDefault(id);
        return instances is not null
            ? [.. instances.Values]
            : [];
    }

    public NonStackableItemInstance? GetItemInstance(Guid id, Guid instanceId)
    {
        var instances = NonStackableItemsData!.GetValueOrDefault(id);
        return instances?.GetValueOrDefault(instanceId);
    }

    public void AddItems(Guid id, int count)
    {
        if (count < 0)
        {
            throw new ArgumentException($"{nameof(count)} is negative.", nameof(count));
        }

        StackableItemsData[id] = StackableItemsData.GetValueOrDefault(id, 0) + count;
    }

    public void AddItems(Guid id, NonStackableItemInstance[] instances)
    {
        var instancesMap = NonStackableItemsData.ComputeIfAbsent(id, id1 => [])!;

        foreach (NonStackableItemInstance instance in instances)
        {
            instancesMap.Add(instance.InstanceId, instance);
        }
    }

    public bool TakeItems(Guid id, int count)
    {
        if (count < 0)
        {
            throw new ArgumentException($"{nameof(count)} is negative.", nameof(count));
        }

        int currentCount = StackableItemsData.GetValueOrDefault(id);
        if (currentCount < count)
        {
            return false;
        }

        StackableItemsData[id] = currentCount - count;
        return true;
    }

    public IEnumerable<NonStackableItemInstance>? TakeItems(Guid id, ReadOnlySpan<Guid> instanceIds)
    {
        var instanceMap = NonStackableItemsData.GetValueOrDefault(id);
        if (instanceMap is null)
        {
            return null;
        }

        var instances = new List<NonStackableItemInstance>(instanceIds.Length);
        foreach (var instanceId in instanceIds)
        {
            if (!instanceMap.Remove(instanceId, out var instance))
            {
                return null;
            }

            instances.Add(instance);
        }

        return instances;
    }

    public async Task MergeWith(InventoryEF other, ValueMerger merger)
    {
        merger.CurrentUserId = Id.ToString();
        merger.CurrentUsername = Account?.Username;

        foreach (var item in other.StackableItemsData)
        {
            if (!StackableItemsData.TryGetValue(item.Key, out var currentValue))
            {
                StackableItemsData.Add(item.Key, item.Value);
            }
            else
            {
                // todo: resolve name
                StackableItemsData[item.Key] = await merger.AutoMergeMax(currentValue, item.Value, $"Inventory item '{item.Key}'");
            }
        }

        foreach (var item in other.NonStackableItemsData)
        {
            if (!NonStackableItemsData.TryGetValue(item.Key, out var currentValue))
            {
                NonStackableItemsData.Add(item.Key, item.Value);
            }
            else
            {
                foreach (var item2 in item.Value)
                {
                    currentValue[item2.Key] = item2.Value;
                }
            }
        }
    }

    public sealed class Legacy : IEquatable<Legacy>
    {
        [JsonInclude, JsonPropertyName("stackableItems")]
        public Dictionary<Guid, int?> StackableItems;
        [JsonInclude, JsonPropertyName("nonStackableItems")]
        public Dictionary<Guid, Dictionary<Guid, NonStackableItemInstance.Legacy>> NonStackableItems;

        public Legacy()
        {
            StackableItems = [];
            NonStackableItems = [];
        }

        public sealed record StackableItem(
            Guid Id,
            int? Count
        );

        public sealed record NonStackableItem(
            Guid Id,
            NonStackableItemInstance[] Instances
        )
        {
            public bool Equals(NonStackableItem? other)
                => other is not null && Id == other.Id && Instances.SequenceEqual(other.Instances);

            public override int GetHashCode()
            {
                var hash = new HashCode();

                hash.Add(Id);

                foreach (var item in Instances)
                {
                    hash.Add(item);
                }

                return hash.ToHashCode();
            }
        }

        public bool Equals(Legacy? other)
            => other is not null &&
            StackableItems.OrderBy(static item => item.Key).Select(item => (Key: item.Key, Value: item.Value)).SequenceEqual(other.StackableItems.OrderBy(static item => item.Key).Select(item => (Key: item.Key, Value: item.Value))) &&
            NonStackableItems.OrderBy(static item => item.Key).Select(item => (Key: item.Key, Value: item.Value)).SequenceEqual(other.NonStackableItems.OrderBy(static item => item.Key).Select(item => (Key: item.Key, Value: item.Value)));

        public override bool Equals(object? obj)
            => Equals(obj as Legacy);

        public override int GetHashCode()
        {
            var hash = new HashCode();

            foreach (var item in StackableItems.OrderBy(static item => item.Key))
            {
                hash.Add(item.Key);
                hash.Add(item.Value);
            }

            foreach (var item in NonStackableItems.OrderBy(static item => item.Key))
            {
                hash.Add(item.Key);
                hash.Add(item.Value);
            }

            return hash.ToHashCode();
        }
    }
}

#region Converter
public sealed class StackableItemValueConverter : Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter<StackableItemData, string>
{
    public StackableItemValueConverter() : base(
        v => JsonSerializer.Serialize(v, DbJsonContext.Default.DictionaryGuidInt32),
        v => JsonSerializer.Deserialize(v, DbJsonContext.Default.DictionaryGuidInt32) ?? new StackableItemData())
    {
    }
}

public sealed class StackableItemDictionaryValueComparer : Microsoft.EntityFrameworkCore.ChangeTracking.ValueComparer<StackableItemData>
{
    public StackableItemDictionaryValueComparer() : base(
        (a, b) => CompareDictionaries(a, b),
        a => GetArrayHashCode(a),
        a => SnapshotDictionary(a))
    {
    }

    public static bool CompareDictionaries(StackableItemData? a, StackableItemData? b)
    {
         if (a == b)
        {
            return true;
        }

        if (a == null || b == null)
        {
            return false;
        }

        if (a.Count != b.Count)
        {
            return false;
        }

        foreach (var kvp in a)
        {
            if (!b.TryGetValue(kvp.Key, out var value2))
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

    public static int GetArrayHashCode(StackableItemData a)
    {
        var hash = new HashCode();
        foreach (var kvp in a.OrderBy(x => x.Key))
        {
            hash.Add(kvp.Key);
            hash.Add(kvp.Value);
        }

        return hash.ToHashCode();
    }

    public static StackableItemData SnapshotDictionary(StackableItemData a)
        => new(a.Select(item => new KeyValuePair<Guid, int>(item.Key, item.Value)));
}

public sealed class NonStackableItemValueConverter : Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter<NonStackableItemData, string>
{
    public NonStackableItemValueConverter() : base(
        v => JsonSerializer.Serialize(v, DbJsonContext.Default.DictionaryGuidDictionaryGuidNonStackableItemInstance),
        v => JsonSerializer.Deserialize(v, DbJsonContext.Default.DictionaryGuidDictionaryGuidNonStackableItemInstance) ?? new NonStackableItemData())
    {
    }
}

public sealed class NonStackableItemDictionaryValueComparer : Microsoft.EntityFrameworkCore.ChangeTracking.ValueComparer<NonStackableItemData>
{
    public NonStackableItemDictionaryValueComparer()
        : base(
            (d1, d2) => OuterDictionariesEqual(d1, d2),
            d => ComputeOuterHashCode(d),
            d => d.ToDictionary(x => x.Key, x => new Dictionary<Guid, NonStackableItemInstance>(x.Value.Select(item => new KeyValuePair<Guid, NonStackableItemInstance>(item.Key, item.Value.DeepCopy())))))
    {
    }

    public static bool OuterDictionariesEqual(NonStackableItemData? d1, NonStackableItemData? d2)
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

    public static bool InnerDictionariesEqual(Dictionary<Guid, NonStackableItemInstance>? d1, Dictionary<Guid, NonStackableItemInstance>? d2)
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

    public static int ComputeOuterHashCode(NonStackableItemData? d)
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
#endregion
