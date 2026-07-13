using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;
using Solace.Common;
using Solace.Common.Utils;

namespace Solace.DB.Models.Player;

public sealed class JournalEF : IEntityWithId<Guid>, IVersionedEntity, IMergeable<JournalEF>
{
    public Guid Id { get; set; }

    public int Version { get; set; } = 1;

    public Account Account { get; set; } = null!;

    public Dictionary<Guid, ItemJournalEntry> Items { get; set; } = [];

    public ItemJournalEntry? GetItem(Guid uuid)
        => Items.GetValueOrDefault(uuid);

    public int AddCollectedItem(Guid uuid, DateTimeOffset timestamp, int count)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);

        var timestampMs = timestamp.ToUnixTimeMilliseconds();

        ItemJournalEntry? itemJournalEntry = Items.GetValueOrDefault(uuid);
        if (itemJournalEntry is null)
        {
            Items[uuid] = new ItemJournalEntry(timestampMs, timestampMs, count);
            return 0;
        }
        else
        {
            Items[uuid] = new ItemJournalEntry(itemJournalEntry.FirstSeen, timestampMs, itemJournalEntry.AmountCollected + count);
            return itemJournalEntry.AmountCollected;
        }
    }

    public async Task MergeWith(JournalEF other, ValueMerger merger)
    {
        merger.CurrentUserId = Id.ToString();
        merger.CurrentUsername = Account?.Username;

        foreach (var item in other.Items)
        {
            if (!Items.TryGetValue(item.Key, out var currentValue))
            {
                Items.Add(item.Key, item.Value);
            }
            else
            {
                currentValue = currentValue with
                {
                    FirstSeen = await merger.AutoMergeMin(currentValue.FirstSeen, item.Value.FirstSeen, $"Journal item first seen '{item.Key}'"),
                    LastSeen = await merger.AutoMergeMax(currentValue.LastSeen, item.Value.LastSeen, $"Journal item last seen '{item.Key}'"),
                    AmountCollected = await merger.AutoMergeMax(currentValue.AmountCollected, item.Value.AmountCollected, $"Journal item amount collected '{item.Key}'"),
                };

                Items[item.Key] = currentValue;
            }
        }
    }

    public sealed record ItemJournalEntry(
        long FirstSeen,
        long LastSeen,
        int AmountCollected
    ) : ICloneable<ItemJournalEntry>
    {
        public ItemJournalEntry DeepCopy()
            => new ItemJournalEntry(this);

        public sealed class Comparer : IEqualityComparer<ItemJournalEntry>
        {
            public static Comparer Instance { get; } = new Comparer();

            private Comparer()
            {
            }

            public bool Equals(ItemJournalEntry? x, ItemJournalEntry? y)
                => x == y || (x?.Equals(y) ?? false);

            public int GetHashCode([DisallowNull] ItemJournalEntry obj)
                => obj.GetHashCode();
        }
    }

    public sealed class Legacy : IEquatable<Legacy>
    {
        [JsonInclude, JsonPropertyName("items")]
        public Dictionary<Guid, ItemJournalEntry> _items;

        public Legacy()
        {
            _items = [];
        }

        [JsonIgnore]
        public Dictionary<Guid, ItemJournalEntry> Items => _items;

        // KVP is not equatable
        public bool Equals(Legacy? other)
            => other is not null && _items.Select(item => (Key: item.Key, Value: item.Value)).OrderBy(item => item.Key).SequenceEqual(other._items.Select(item => (Key: item.Key, Value: item.Value)).OrderBy(item => item.Key));

        public override bool Equals(object? obj)
            => Equals(obj as Legacy);

        public override int GetHashCode()
        {
            var hash = new HashCode();

            foreach (var item in _items.OrderBy(item => item.Key))
            {
                hash.Add(item.Key);
                hash.Add(item.Value);
            }

            return hash.ToHashCode();
        }

        public sealed record ItemJournalEntry(
            long FirstSeen,
            long LastSeen,
            int AmountCollected
        );
    }
}

#region Converter
public sealed class JournalValueConverter : Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter<Dictionary<Guid, JournalEF.ItemJournalEntry>, string>
{
    public JournalValueConverter() : base(
        v => JsonSerializer.Serialize(v, DbJsonContext.Default.DictionaryGuidItemJournalEntry),
        v => JsonSerializer.Deserialize(v, DbJsonContext.Default.DictionaryGuidItemJournalEntry) ?? new Dictionary<Guid, JournalEF.ItemJournalEntry>())
    {
    }
}

public sealed class JournalDictionaryValueComparer : Microsoft.EntityFrameworkCore.ChangeTracking.ValueComparer<Dictionary<Guid, JournalEF.ItemJournalEntry>>
{
    public JournalDictionaryValueComparer() : base(
        (a, b) => CompareDictionaries(a, b),
        a => GetArrayHashCode(a),
        a => SnapshotDictionary(a))
    {
    }

    public static bool CompareDictionaries(Dictionary<Guid, JournalEF.ItemJournalEntry>? a, Dictionary<Guid, JournalEF.ItemJournalEntry>? b)
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

            if (!JournalEF.ItemJournalEntry.Comparer.Instance.Equals(kvp.Value, value2))
            {
                return false;
            }
        }

        return true;
    }

    public static int GetArrayHashCode(Dictionary<Guid, JournalEF.ItemJournalEntry> a)
    {
        var hash = new HashCode();
        foreach (var kvp in a.OrderBy(x => x.Key))
        {
            hash.Add(kvp.Key);
            hash.Add(kvp.Value, JournalEF.ItemJournalEntry.Comparer.Instance);
        }

        return hash.ToHashCode();
    }

    public static Dictionary<Guid, JournalEF.ItemJournalEntry> SnapshotDictionary(Dictionary<Guid, JournalEF.ItemJournalEntry> a)
        => new(a.Select(item => new KeyValuePair<Guid, JournalEF.ItemJournalEntry>(item.Key, item.Value.DeepCopy())));
}
#endregion
