using System.Text.Json.Serialization;
using Solace.AuthServer.Utils;
using Solace.Common.Asp.Json;

namespace Solace.AuthServer.Features.PlayfabApi.Catalog;

[ForcePascalCase]
[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
public sealed record CatalogItem(
    CatalogItem.Entity SourceEntity,
    CatalogItem.Entity SourceEntityKey,
    Guid Id,
    string Type,
    IEnumerable<CatalogItem.AlternateId> AlternateIds,
    Guid? FriendlyId,
    Dictionary<string, string> Title,
    Dictionary<string, string> Description,
    Dictionary<string, CatalogItem.KeywordValues> Keywords,
    string ContentType,
    CatalogItem.Entity CreatorEntityKey,
    CatalogItem.Entity CreatorEntity,
    bool? IsStackable, // TODO: ??? only used for ruby offer, always false
    IEnumerable<string> Platforms,
    IEnumerable<string> Tags,
    [property: JsonConverter(typeof(UtcDateTimeConverter))] DateTime CreationDate,
    [property: JsonConverter(typeof(UtcDateTimeConverter))] DateTime LastModifiedDate,
    [property: JsonConverter(typeof(UtcDateTimeConverter))] DateTime? StartDate,
    IEnumerable<object> Contents,
    IEnumerable<CatalogItem.Image> Images,
    IEnumerable<CatalogItem.ItemReference> ItemReferences,
    CatalogItem.PriceR? Price,
    CatalogItem.PriceR? PriceOptions,
    IEnumerable<object> DeepLinks,
    CatalogItem.DisplayPropertiesR DisplayProperties,
    string? ETag = null
)
{
    [ForcePascalCase]
    public sealed record Entity(
        string Id,
        string Type,
        string TypeString
    );

    [ForcePascalCase]
    public sealed record AlternateId(
        string Type,
        Guid Value
    );

    [ForcePascalCase]
    public sealed record KeywordValues(
        IEnumerable<string> Values
    );

    [ForcePascalCase]
    public sealed record Image(
        string Id,
        string Tag,
        string Type,
        string Url
    );

    [ForcePascalCase]
    public sealed record ItemReference(
        Guid Id,
        int Amount
    );

    [ForcePascalCase]
    public sealed record PriceR(
        PriceR.Price[] Prices,
        PriceR.Price[] RealPrices
    )
    {
        [ForcePascalCase]
        public sealed record Price(
            CurrencyAmount[] Amounts
        );
    }

    [ForcePascalCase]
    public sealed record CurrencyAmount(
        Guid CurrencyId,
        Guid Id,
        Guid ItemId,
        int Amount
    );

    [ForcePascalCase]
    public sealed record QueryManifestContent(
        string Id,
        string Url,
        Version MaxClientVersion,
        Version MinClientVersion,
        IEnumerable<string> Tags,
        string Type
    );

    [JsonNamingPolicy(JsonKnownNamingPolicy.CamelCase)]
    public sealed record PackIdentity(
        string Type,
        Guid Uuid,
        Version Version
    );

    [JsonNamingPolicy(JsonKnownNamingPolicy.CamelCase)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public sealed record DisplayPropertiesR(
        // query manifest
        Version? MinClientVersion = null,
        Version? MaxClientVersion = null,
        IEnumerable<DisplayPropertiesR.Tab>? Tabs = null,
        IEnumerable<string>? GlobalNotSearchQueryTags = null,

        // buildplate, inventory item, persona
        int? Price = null,
        string? Rarity = null,
        IEnumerable<PackIdentity>? PackIdentity = null,

        // buildplate, persona
        string? CreatorName = null,
        bool? Purchasable = null,

        // buildplate
        Guid? BuildPlateId = null,
        string? BuildPlateSize = null,
        [property: JsonNumberHandling(JsonNumberHandling.WriteAsString)] int? BuildPlateUnlockLevel = null,

        // inventory item
        Guid? ItemId = null,
        int? Amount = null,

        // persona
        Guid? OfferId = null,
        string? PieceType = null
    )
    {
        public static DisplayPropertiesR CreateQueryManifest(Version minClientVersion, Version maxClientVersion, IEnumerable<Tab> tabs, IEnumerable<string> globalNotSearchQueryTags)
            => new(MinClientVersion: minClientVersion, MaxClientVersion: maxClientVersion, Tabs: tabs, GlobalNotSearchQueryTags: globalNotSearchQueryTags);

        public static DisplayPropertiesR CreateBuildplate(string creatorName, int price, bool purchasable, string rarity, IEnumerable<PackIdentity> packIdentity, Guid buildPlateId, string buildPlateSize, int buildPlateUnlockLevel)
            => new(CreatorName: creatorName, Price: price, Purchasable: purchasable, Rarity: rarity, PackIdentity: packIdentity, BuildPlateId: buildPlateId, BuildPlateSize: buildPlateSize, BuildPlateUnlockLevel: buildPlateUnlockLevel);

        public static DisplayPropertiesR CreateInventoryItem(int price, string rarity, IEnumerable<PackIdentity> packIdentity, Guid itemId, int amount)
            => new(Price: price, Rarity: rarity, PackIdentity: packIdentity, ItemId: itemId, Amount: amount);

        public static DisplayPropertiesR CreatePersona(string creatorName, int price, bool purchasable, string rarity, IEnumerable<PackIdentity> packIdentity, Guid offerId, string pieceType)
            => new(CreatorName: creatorName, Price: price, Purchasable: purchasable, Rarity: rarity, PackIdentity: packIdentity, OfferId: offerId, PieceType: pieceType);

        [JsonNamingPolicy(JsonKnownNamingPolicy.CamelCase)]
        public sealed record Tab(
            IEnumerable<Tab.ScreenLayoutQuery> ScreenLayoutQueries,
            string TabIcon,
            string TabTitle,
            string TabId
        )
        {
            [JsonNamingPolicy(JsonKnownNamingPolicy.SnakeCaseLower)]
            [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
            public sealed record ScreenLayoutQuery(
                object? ColumnRectangle,
                object? ColumnSquare,
                object? ColumnGrid,
                IEnumerable<ScreenLayoutQuery.Query> Queries,
                [property: JsonPropertyName("componentId")] Guid ComponentId
            )
            {
                [JsonNamingPolicy(JsonKnownNamingPolicy.CamelCase)]
                public sealed record Query(
                    [property: JsonPropertyName("productIds")] IEnumerable<string> ProductIds,
                    IEnumerable<string> QueryContentTypes,
                    int TopCount
                );
            }
        }
    }
}