using System.Diagnostics.CodeAnalysis;
using Solace.Common;

namespace Solace.Db.Migrator.Old.Earth.Models.Global;

public sealed class SharedBuildplateEF : IEntityWithId<Guid>
{
    public Guid Id { get; set; }

    public Guid AccountId { get; set; }

    public Account Account { get; set; } = null!;

    public required int Size { get; set; }

    public required int Offset { get; set; }

    public required int Scale { get; set; }

    public required bool Night { get; set; }

    public required long Created { get; set; }

    public required long BuildplateLastModifed { get; set; }

    public required long LastViewed { get; set; }

    public required int NumberOfTimesViewed { get; set; }

    public HotbarItem?[] Hotbar { get; set; } = new HotbarItem[7];

    public required string ServerDataObjectId { get; set; }

    public sealed record HotbarItem(
        string Uuid,
        int Count,
        string? InstanceId,
        int Wear
    ) : ICloneable<HotbarItem>
    {
        public HotbarItem DeepCopy()
            => new(this);

        public sealed class Comparer : IEqualityComparer<HotbarItem>
        {
            public static Comparer Instance { get; } = new Comparer();

            private Comparer()
            {
            }

            public bool Equals(HotbarItem? x, HotbarItem? y)
                => x == y || (x?.Equals(y) ?? false);

            public int GetHashCode([DisallowNull] HotbarItem obj)
                => obj.GetHashCode();
        }
    }
}
