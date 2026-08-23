using System.Diagnostics.CodeAnalysis;
using Solace.Common;

namespace Solace.Db.Earth.Models.Player;

public sealed class HotbarEF : IEntityWithId<Guid>
{
    public required Guid Id { get; set; }

    public ProfileEF Profile { get; set; } = null!;

    public Item?[] Items { get; set; } = new Item[7];

    public sealed record Item(
        Guid Uuid,
        int Count,
        Guid? InstanceId
    ) : ICloneable<Item>
    {
        public Item DeepCopy()
            => new(this);
    }
}
