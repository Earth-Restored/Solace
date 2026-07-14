using Solace.Common.Utils;

namespace Solace.DB.Models.Player.Workshop;

public sealed class CraftingSlotsEF : IEntityWithId<Guid>
{
    public Guid Id { get; set; }

    public Account Account { get; set; } = null!;

    public CraftingSlotEF[] Slots { get; set; } = [new CraftingSlotEF(), new CraftingSlotEF(), new CraftingSlotEF()];
}
