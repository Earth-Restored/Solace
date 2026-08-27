namespace Solace.Db.Migrator.Old.Earth.Models.Player.Workshop;

public sealed class CraftingSlotsEF : IEntityWithId<Guid>, IVersionedEntity
{
    public Guid Id { get; set; }

    public int Version { get; set; } = 1;

    public Account Account { get; set; } = null!;

    public CraftingSlotEF[] Slots { get; set; } = [new CraftingSlotEF(), new CraftingSlotEF() { Locked = true, }, new CraftingSlotEF() { Locked = true, }];
}
