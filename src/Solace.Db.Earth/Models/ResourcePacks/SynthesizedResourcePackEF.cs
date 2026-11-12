namespace Solace.Db.Earth.Models.ResourcePacks;

public sealed class SynthesizedResourcePackEF
{
    public Guid Id { get; set; }

    public int BuildNumber { get; set; }

    public required string VersionString { get; set; }

    // hash of the base rp file this rp was generated from, used to detect when the base rp changes
    public required string ContentHash { get; set; }
}
