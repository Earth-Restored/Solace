namespace Solace.Db.Playfab.Models.Items;

public abstract class ItemDataEF
{
    public Guid Id { get; set; }

    public ItemEF Item { get; set; } = null!;
}
