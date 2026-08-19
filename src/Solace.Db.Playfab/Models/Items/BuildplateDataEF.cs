namespace Solace.Db.Playfab.Models.Items;

public sealed class BuildplateDataEF : ItemDataEF
{
    public Guid BuildplateId { get; set; }

    public int Cost { get; set; }

    public BuildplateSizeEF Size { get; set; }

    public int UnlockLevel { get; set; }

    public RarityEF Rarity { get; set; }

    public Version Version { get; set; } = new Version(1, 0, 0);
}
