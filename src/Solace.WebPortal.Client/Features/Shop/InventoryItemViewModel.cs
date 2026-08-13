namespace Solace.WebPortal.Client.Features.Shop;

public sealed class InventoryItemViewModel
{
    public int Cost { get; set; }

    public int Amount { get; set; } = 1;

    public string Rarity { get; set; } = "Common";

    public string Version { get; set; } = "1.0.0";
}
