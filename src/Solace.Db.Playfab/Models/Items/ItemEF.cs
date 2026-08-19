namespace Solace.Db.Playfab.Models.Items;

public sealed class ItemEF
{
    public Guid Id { get; set; }

    public Guid? FriendlyId { get; set; }

    public bool Purchasable { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public Guid? ThumbnailImageId { get; set; }

    public DateTimeOffset CreationDate { get; set; }

    public DateTimeOffset LastModifiedDate { get; set; }

    public DateTimeOffset StartDate { get; set; }

    public string SourceEntityId { get; set; } = string.Empty;

    public string CreatorEntityId { get; set; } = string.Empty;

    public ItemDataEF? Data { get; set; }

    public List<string> Tags { get; set; } = [];

    public Dictionary<string, KeywordValuesEF> Keywords { get; set; } = [with(StringComparer.Ordinal)];

    public Dictionary<string, string> TitleTranslations { get; set; } = [with(StringComparer.Ordinal)];

    public Dictionary<string, string> DescriptionTranslations { get; set; } = [with(StringComparer.Ordinal)];

    public List<ItemReferenceEF> ItemReferences { get; set; } = [];
}
