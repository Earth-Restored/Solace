namespace Solace.Db.Earth.Models.Player.Workshop;

public sealed class SmeltingSlotsEF : IEntityWithId<Guid>
{
    public Guid Id { get; set; }

    public ProfileEF Profile { get; set; } = null!;

    public SmeltingSlotEF[] Slots { get; set; } = [new SmeltingSlotEF(), new SmeltingSlotEF(), new SmeltingSlotEF()];
}
