using System.Diagnostics.CodeAnalysis;
using Solace.Common;

namespace Solace.Db.Migrator.Old.Earth.Models.Player;

public sealed class HotbarEF : IEntityWithId<Guid>, IVersionedEntity
{
    public Guid Id { get; set; }

    public int Version { get; set; } = 1;

    public Account Account { get; set; } = null!;

    public Item?[] Items { get; set; } = new Item[7];

    public sealed record Item(
        string Uuid,
        int Count,
        string? InstanceId
    ) : ICloneable<Item>
    {
        public Item DeepCopy()
            => new(this);

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
}
