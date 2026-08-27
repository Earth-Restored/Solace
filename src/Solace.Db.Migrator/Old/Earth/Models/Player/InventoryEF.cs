using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;
using Solace.Db.Migrator.Old.Earth.Models.Common;

namespace Solace.Db.Migrator.Old.Earth.Models.Player;

public sealed class InventoryEF : IEntityWithId<Guid>, IVersionedEntity
{
    public Guid Id { get; set; }

    public int Version { get; set; } = 1;

    public Account Account { get; set; } = null!;

    // id to count
    public Dictionary<string, int> StackableItemsData { get; set; } = [with(StringComparer.Ordinal)];

    // id to (instanceId to instance)
    public Dictionary<string, Dictionary<string, NonStackableItemInstance>> NonStackableItemsData { get; set; } = [with(StringComparer.Ordinal)];

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
}