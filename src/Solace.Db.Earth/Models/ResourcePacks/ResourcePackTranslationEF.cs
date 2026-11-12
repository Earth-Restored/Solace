namespace Solace.Db.Earth.Models.ResourcePacks;

public sealed class ResourcePackTranslationEF
{
    public required string Language { get; set; }

    public required string Key { get; set; }

    public required string Value { get; set; }
}
