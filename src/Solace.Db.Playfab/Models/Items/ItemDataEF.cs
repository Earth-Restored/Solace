namespace Solace.Db.Playfab.Models.Items;

public abstract class ItemDataEF
{
    [Obsolete("Make sure you didn't mean to use BuildplateId/ItemId.", error: false)]
    public Guid Id { get; set; }

    public ItemEF Item { get; set; } = null!;
}
