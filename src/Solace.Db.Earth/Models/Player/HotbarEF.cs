using System.Diagnostics.CodeAnalysis;
using Solace.Common;

namespace Solace.Db.Earth.Models.Player;

public sealed class HotbarEF : IEntityWithId<Guid>
{
    public required Guid Id { get; set; }

    public Account Account { get; set; } = null!;

    public Item?[] Items { get; set; } = new Item[7];

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
}
