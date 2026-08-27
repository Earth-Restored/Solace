using System.Diagnostics.CodeAnalysis;
using Solace.Common;

namespace Solace.Db.Migrator.Old.Earth.Models.Player;

public sealed class JournalEF : IEntityWithId<Guid>, IVersionedEntity
{
    public Guid Id { get; set; }

    public int Version { get; set; } = 1;

    public Account Account { get; set; } = null!;

    public Dictionary<string, ItemJournalEntry> Items { get; set; } = [with(StringComparer.Ordinal)];

    public sealed record ItemJournalEntry(
        long FirstSeen,
        long LastSeen,
        int AmountCollected
    ) : ICloneable<ItemJournalEntry>
    {
        public ItemJournalEntry DeepCopy()
            => new(this);

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
}