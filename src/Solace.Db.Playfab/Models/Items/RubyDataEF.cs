namespace Solace.Db.Playfab.Models.Items;

public class RubyDataEF : ItemDataEF
{
    public int CoinCount { get; set; }

    public int? BonusCoinCount { get; set; }

    public string Sku { get; set; } = string.Empty;

    public string OriginalCreatorId { get; set; } = string.Empty;
}
