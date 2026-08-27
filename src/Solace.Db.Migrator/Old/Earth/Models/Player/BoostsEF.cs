using System.Diagnostics.CodeAnalysis;
using Solace.Common;

namespace Solace.Db.Migrator.Old.Earth.Models.Player;

public sealed class BoostsEF : IEntityWithId<Guid>, IVersionedEntity
{
    public Guid Id { get; set; }

    public int Version { get; set; } = 1;

    public Account Account { get; set; } = null!;

    public ActiveBoost?[] ActiveBoosts { get; set; } = new ActiveBoost[5];

    public sealed record ActiveBoost(
        string InstanceId,
        string ItemId,
        long StartTime,
        long Duration
    ) : ICloneable<ActiveBoost>
    {
        public ActiveBoost DeepCopy()
            => new(this);

        public sealed class Comparer : IEqualityComparer<ActiveBoost>
        {
            public static Comparer Instance { get; } = new Comparer();

            private Comparer()
            {
            }

            public bool Equals(ActiveBoost? x, ActiveBoost? y)
                => x == y || (x?.Equals(y) ?? false);

            public int GetHashCode([DisallowNull] ActiveBoost obj)
                => obj.GetHashCode();
        }
    }
}
