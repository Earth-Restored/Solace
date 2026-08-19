using System.ComponentModel.DataAnnotations;

namespace Solace.WebPortal.Client.Features.Store.Models;

public sealed class InventoryItemViewModel
{
    [Required]
    public Guid ItemId { get; set; }

    public string? ItemName { get; set; }

    public int Cost { get; set; }

    public int Amount { get; set; } = 1;

    public Rarity Rarity { get; set; } = Rarity.Common;

    public string Version { get; set; } = "1.0.0";
}
