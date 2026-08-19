using System.ComponentModel.DataAnnotations;

namespace Solace.WebPortal.Client.Features.Store.Models;

public class BuildplateViewModel
{
    [Required]
    public Guid BuildplateId { get; set; }

    public string? BuildplateName { get; set; }

    public int Cost { get; set; }

    public int UnlockLevel { get; set; } = 1;

    public bool Is1Player { get; set; } = true;

    public HashSet<string> Tags = [];

    public Rarity Rarity { get; set; } = Rarity.Common;

    public string Version { get; set; } = "1.0.0";
}
