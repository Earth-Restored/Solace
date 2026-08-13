using Solace.Common;

namespace Solace.Db.Playfab.Models.Items;

public sealed class ItemReferenceEF : IEquatable<ItemReferenceEF>, ICloneable<ItemReferenceEF>
{
    public Guid Id { get; set; }

    public int Amount { get; set; }

    public ItemReferenceEF DeepCopy()
        => new()
        {
            Id = Id,
            Amount = Amount
        };

    public bool Equals(ItemReferenceEF? other)
        => other is not null && Amount == other.Amount && Id == other.Id;

    public override bool Equals(object? obj)
        => Equals(obj as ItemReferenceEF);

    public override int GetHashCode()
        => HashCode.Combine(Id, Amount);
}
