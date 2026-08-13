using Solace.Db.Playfab.Models.Tabs;

namespace Solace.Db.Playfab.Models.Items;

public sealed class QueryManifestDataEF : ItemDataEF
{
    public Version MinClientVersion { get; set; } = new Version(0, 25, 0);

    public Version MaxClientVersion { get; set; } = new Version(1, 0, 20);

    public List<TabEF> Tabs { get; set; } = [];

    public List<string> GlobalNotSearchQueryTags { get; set; } = [];
}
