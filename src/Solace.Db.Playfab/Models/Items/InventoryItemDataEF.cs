namespace Solace.Db.Playfab.Models.Items;

public sealed class InventoryItemDataEF : ItemDataEF
{
    public Guid ItemId { get; set; }

    public int Cost { get; set; }

    public int Amount { get; set; }

    public RarityEF Rarity { get; set; }

    public Version Version { get; set; } = new Version(1, 0, 0);
}
