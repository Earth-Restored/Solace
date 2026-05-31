namespace Solace.StaticData;

public static class AdventureMapIcons
{
    public static string ToClientMapIcon(string icon, string rarity)
    {
        if (icon.StartsWith("genoa:adventure_generic_map", StringComparison.OrdinalIgnoreCase))
        {
            return icon;
        }

        return rarity.ToUpperInvariant() switch
        {
            "COMMON" => "genoa:adventure_generic_map",
            "UNCOMMON" or "RARE" => "genoa:adventure_generic_map_b",
            "EPIC" or "LEGENDARY" or "OOBE" => "genoa:adventure_generic_map_c",
            _ => "genoa:adventure_generic_map"
        };
    }
}
