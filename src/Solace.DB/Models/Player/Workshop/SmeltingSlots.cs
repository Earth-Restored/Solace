namespace Solace.DB.Models.Player.Workshop;

public sealed class SmeltingSlotsEF : IVersionedEntity
{
    public Guid Id { get; set; }

    public int Version { get; set; } = 1;

    public Account Account { get; set; } = null!;

    public SmeltingSlot[] Slots { get; set; } = [new SmeltingSlot(), new SmeltingSlot(), new SmeltingSlot()];

    public sealed class Legacy : IEquatable<Legacy>
    {
        public SmeltingSlot.Legacy[] Slots { get; init; }

        public Legacy()
        {
            Slots = [new SmeltingSlot.Legacy(), new SmeltingSlot.Legacy(), new SmeltingSlot.Legacy()];
        }

        public bool Equals(Legacy? other)
            => other is not null && Slots.SequenceEqual(other.Slots);

        public override bool Equals(object? obj)
            => Equals(obj as Legacy);

        public override int GetHashCode()
        {
            var hash = new HashCode();

            foreach (var item in Slots)
            {
                hash.Add(item);
            }

            return hash.ToHashCode();
        }
    }
}
