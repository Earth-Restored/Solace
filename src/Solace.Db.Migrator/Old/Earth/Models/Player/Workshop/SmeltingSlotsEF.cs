namespace Solace.Db.Migrator.Old.Earth.Models.Player.Workshop;

public sealed class SmeltingSlotsEF : IEntityWithId<Guid>, IVersionedEntity
{
    public Guid Id { get; set; }

    public int Version { get; set; } = 1;

    public Account Account { get; set; } = null!;

    public SmeltingSlot[] Slots { get; set; } = [new SmeltingSlot(), new SmeltingSlot() { Locked = true, }, new SmeltingSlot() { Locked = true, }];
}
