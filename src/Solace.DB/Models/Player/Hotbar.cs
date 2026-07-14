using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using BitcoderCZ.Utils;
using Solace.Common;
using Solace.Common.Utils;

namespace Solace.DB.Models.Player;

public sealed class HotbarEF : IEntityWithId<Guid>
{
    public required Guid Id { get; set; }

    public Account Account { get; set; } = null!;

    public Item?[] Items { get; set; } = new Item[7];

    public sealed record Item(
        Guid Uuid,
        int Count,
        Guid? InstanceId
    );
}
