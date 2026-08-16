using System.ComponentModel.DataAnnotations;

namespace Solace.WebPortal.Client.Features.Shop.Models;

public class BuildplateViewModel
{
    [Required]
    public Guid BuildplateId { get; set; }

    public string? BuildplateName { get; set; }

    public int Cost { get; set; }

    public int UnlockLevel { get; set; } = 1;

    public Rarity Rarity { get; set; } = Rarity.Common;

    public string Version { get; set; } = "1.0.0";
}
