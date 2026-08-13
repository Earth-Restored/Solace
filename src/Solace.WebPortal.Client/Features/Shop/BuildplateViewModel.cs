namespace Solace.WebPortal.Client.Features.Shop;

public class BuildplateViewModel
{
    public int Cost { get; set; }

    public BuidplateSize Size { get; set; } = BuidplateSize.Small;

    public int UnlockLevel { get; set; } = 1;

    public string Rarity { get; set; } = "Common";

    public string Version { get; set; } = "1.0.0";
}
