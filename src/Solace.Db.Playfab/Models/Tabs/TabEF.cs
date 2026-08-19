namespace Solace.Db.Playfab.Models.Tabs;

public sealed class TabEF
{
    public int TabIndex { get; set; }

    public string TabId { get; set; } = string.Empty;

    public string TabTitle { get; set; } = string.Empty;

    public string TabIcon { get; set; } = string.Empty;

    public List<ScreenLayoutQueryEF> ScreenLayoutQueries { get; set; } = [];
}
