namespace Solace.DB.Earth.Models.Player.Workshop;

public sealed class SmeltingSlotsEF : IEntityWithId<Guid>
{
    public Guid Id { get; set; }

    public Account Account { get; set; } = null!;

    public SmeltingSlotEF[] Slots { get; set; } = [new SmeltingSlotEF(), new SmeltingSlotEF(), new SmeltingSlotEF()];
}
