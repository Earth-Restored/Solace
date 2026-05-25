using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;
using Solace.Common.Utils;
using Solace.DB.Models.Common;

namespace Solace.DB.Models.Player;

public sealed class InventoryEF : IEntityWithId<Guid>, IVersionedEntity, IMergeable<InventoryEF>
{
    public Guid Id { get; set; }

    public int Version { get; set; } = 1;

    public Account Account { get; set; } = null!;

    public Dictionary<string, int> StackableItemsData { get; set; } = [];

    public Dictionary<string, Dictionary<string, NonStackableItemInstance>> NonStackableItemsData { get; set; } = [];

    [JsonIgnore, NotMapped]
    public IEnumerable<StackableItem> StackableItems => StackableItemsData.Select(item => new StackableItem(item.Key, item.Value));

    [JsonIgnore, NotMapped]
    public IEnumerable<NonStackableItem> NonStackableItems => NonStackableItemsData.Select(item => new NonStackableItem(item.Key, [.. item.Value.Values]));

    public sealed record StackableItem(
        string Id,
        int Count
    );

    public sealed record NonStackableItem(
        string Id,
        NonStackableItemInstance[] Instances
    );

    public int GetItemCount(string id)
    {
        if (StackableItemsData.TryGetValue(id, out var count))
        {
            return count;
        }

        Dictionary<string, NonStackableItemInstance>? instances = NonStackableItemsData!.GetValueOrDefault(id);

        return instances is not null
            ? instances.Count
            : 0;
    }

    public NonStackableItemInstance[] GetItemInstances(string id)
    {
        Dictionary<string, NonStackableItemInstance>? instances = NonStackableItemsData!.GetValueOrDefault(id);
        return instances is not null
            ? [.. instances.Values]
            : [];
    }

    public NonStackableItemInstance? GetItemInstance(string id, string instanceId)
    {
        Dictionary<string, NonStackableItemInstance>? instances = NonStackableItemsData!.GetValueOrDefault(id);
        return instances?.GetValueOrDefault(instanceId);
    }

    public void AddItems(string id, int count)
    {
        if (count < 0)
        {
            throw new ArgumentException($"{nameof(count)} is negative.", nameof(count));
        }

        StackableItemsData[id] = StackableItemsData.GetValueOrDefault(id, 0) + count;
    }

    public void AddItems(string id, NonStackableItemInstance[] instances)
    {
        Dictionary<string, NonStackableItemInstance> instancesMap = NonStackableItemsData.ComputeIfAbsent(id, id1 => [])!;

        foreach (NonStackableItemInstance instance in instances)
        {
            instancesMap.Add(instance.InstanceId, instance);
        }
    }

    public bool TakeItems(string id, int count)
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

    public IEnumerable<NonStackableItemInstance>? TakeItems(string id, ReadOnlySpan<string> instanceIds)
    {
        Dictionary<string, NonStackableItemInstance>? instanceMap = NonStackableItemsData.GetValueOrDefault(id);
        if (instanceMap is null)
        {
            return null;
        }

        var instances = new List<NonStackableItemInstance>(instanceIds.Length);
        foreach (string instanceId in instanceIds)
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
        public Dictionary<string, int?> StackableItems;
        [JsonInclude, JsonPropertyName("nonStackableItems")]
        public Dictionary<string, Dictionary<string, NonStackableItemInstance.Legacy>> NonStackableItems;

        public Legacy()
        {
            StackableItems = [];
            NonStackableItems = [];
        }

        public sealed record StackableItem(
            string Id,
            int? Count
        );

        public sealed record NonStackableItem(
            string Id,
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
            StackableItems.OrderBy(static item => item.Key, StringComparer.Ordinal).Select(item => (Key: item.Key, Value: item.Value)).SequenceEqual(other.StackableItems.OrderBy(static item => item.Key, StringComparer.Ordinal).Select(item => (Key: item.Key, Value: item.Value))) &&
            NonStackableItems.OrderBy(static item => item.Key, StringComparer.Ordinal).Select(item => (Key: item.Key, Value: item.Value)).SequenceEqual(other.NonStackableItems.OrderBy(static item => item.Key, StringComparer.Ordinal).Select(item => (Key: item.Key, Value: item.Value)));

        public override bool Equals(object? obj)
            => Equals(obj as Legacy);

        public override int GetHashCode()
        {
            var hash = new HashCode();

            foreach (var item in StackableItems.OrderBy(static item => item.Key, StringComparer.Ordinal))
            {
                hash.Add(item.Key);
                hash.Add(item.Value);
            }

            foreach (var item in NonStackableItems.OrderBy(static item => item.Key, StringComparer.Ordinal))
            {
                hash.Add(item.Key);
                hash.Add(item.Value);
            }

            return hash.ToHashCode();
        }
    }
}