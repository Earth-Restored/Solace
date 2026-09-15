namespace Solace.Db.Earth.Models.Player.Workshop;

public sealed class CraftingSlotsEF : IEntityWithId<Guid>
{
    public Guid Id { get; set; }

    public ProfileEF Profile { get; set; } = null!;

    public CraftingSlotEF[] Slots { get; set; } = [new CraftingSlotEF(), new CraftingSlotEF() { Locked = true, }, new CraftingSlotEF() { Locked = true, }];
}
