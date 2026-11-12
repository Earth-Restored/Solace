namespace Solace.Db.Earth.Models.ResourcePacks;

public sealed class ResourcePackAssetEF
{
    public required string Path { get; set; }

    public required byte[] Data { get; set; }
}
