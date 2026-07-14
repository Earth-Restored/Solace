using System.Text.Json;
using Solace.Common.Utils;

namespace Solace.DB.Models.Player.Workshop;

public sealed class SmeltingSlotsEF : IEntityWithId<Guid>
{
    public Guid Id { get; set; }

    public Account Account { get; set; } = null!;

    public SmeltingSlot[] Slots { get; set; } = [new SmeltingSlot(), new SmeltingSlot(), new SmeltingSlot()];
}
