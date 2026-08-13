using System.ComponentModel.DataAnnotations;

namespace Solace.WebPortal.Client.Features.Shop;

public sealed class ItemViewModel
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required] public string Title { get; set; } = string.Empty;

    [Required] public string Description { get; set; } = string.Empty;

    public bool Purchasable { get; set; } = true;

    public string ItemDataType { get; set; } = "Buildplate";

    public BuildplateViewModel Buildplate { get; set; } = new();

    public InventoryItemViewModel InventoryItem { get; set; } = new();
}
