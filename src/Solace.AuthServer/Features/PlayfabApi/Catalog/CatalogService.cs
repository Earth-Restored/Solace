using System.Collections.Frozen;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Microsoft.OData.Edm;
using Microsoft.OData.ModelBuilder;
using OData2Linq;

namespace Solace.AuthServer.Features.PlayfabApi.Catalog;

public sealed class CatalogService
{
    private readonly CatalogItem[] _itemData;
    private readonly FrozenDictionary<Guid, CatalogItem> _itemById;
    private readonly IEdmModel _edmModel;

    public CatalogService(StaticData.StaticDataProvider staticData)
    {
        _itemData = CreateItemData(staticData);

        _itemById = _itemData.ToFrozenDictionary(item => item.Id);

        _edmModel = CreateCatalogItemEdmModel();
    }

    public ODataQuery<CatalogItem> CreateItemsQuery()
        => _itemData
        .AsQueryable()
        .OData(settings =>
        {
            settings.EnableCaseInsensitive = true;
            settings.ValidationSettings.MaxNodeCount = 10000;
        }, _edmModel);

    public bool TryGetItem(Guid id, [MaybeNullWhen(false)] out CatalogItem catalogItem)
        => _itemById.TryGetValue(id, out catalogItem);

    public CatalogItem FixItemUrls(CatalogItem item, HttpRequest request)
    {
        var host = $"{(request.IsHttps ? "https://" : "http://")}{request.Host.Value}";

        return item with
        {
            Images = item.Images.Select(image => image with { Url = host + image.Url, }),
            Contents = item.Contents.Select(content => content switch
            {
                CatalogItem.QueryManifestContent qmContent => qmContent with { Url = host + qmContent.Url, },
                _ => content,
            }),
        };
    }

    public CatalogItem StaticDataItemToCatalogItem(StaticData.Playfab.Item item)
    {
        var price = item.Data switch
        {
            StaticData.Playfab.Item.BuildplateData data => new CatalogItem.PriceR([
                new([
                    new(PlayfabApiUtils.RubyCurrencyId, PlayfabApiUtils.RubyCurrencyId, PlayfabApiUtils.RubyCurrencyId, data.Cost),
                ]),
            ], []),
            StaticData.Playfab.Item.InventoryItemData data => new CatalogItem.PriceR([
                new([
                    new(PlayfabApiUtils.RubyCurrencyId, PlayfabApiUtils.RubyCurrencyId, PlayfabApiUtils.RubyCurrencyId, data.Cost),
                ]),
            ], []),
            StaticData.Playfab.Item.RubyData => null,
            StaticData.Playfab.Item.QueryManifestData => null,
            _ => throw new UnreachableException(),
        };

        var creatorEntityType = item.Data is StaticData.Playfab.Item.RubyData ? "master_player_account" : "title_player_account";

        return new CatalogItem(
            new CatalogItem.Entity(item.SourceEntityId, "namespace", "namespace"),
            new CatalogItem.Entity(item.SourceEntityId, "namespace", "namespace"),
            item.Id,
            item.Data switch
            {
                StaticData.Playfab.Item.BuildplateData => "bundle",
                StaticData.Playfab.Item.InventoryItemData => "bundle",
                StaticData.Playfab.Item.RubyData => "catalogItem",
                StaticData.Playfab.Item.QueryManifestData => "catalogItem",
                _ => throw new UnreachableException(),
            },
            item.FriendlyId is null ? [] : [new("FriendlyId", item.FriendlyId.Value)],
            item.FriendlyId,
            ((IEnumerable<KeyValuePair<string, string>>)[new("NEUTRAL", item.Title), .. item.TitleTranslations, new("neutral", item.Title)])
                .ToDictionary(),
            ((IEnumerable<KeyValuePair<string, string>>)[new("NEUTRAL", item.Description), .. item.DescriptionTranslations, new("neutral", item.Description)])
                .ToDictionary(),
            item.Keywords.ToDictionary(item => item.Key, item => new CatalogItem.KeywordValues(item.Value.Values)),
            item.Data switch
            {
                StaticData.Playfab.Item.BuildplateData => "BuildplateOffer",
                StaticData.Playfab.Item.InventoryItemData => "InventoryItemOffer",
                StaticData.Playfab.Item.RubyData => "RubyOffer",
                StaticData.Playfab.Item.QueryManifestData => "GenoaQueryManifest_V0.0.3",
                _ => throw new UnreachableException(),
            },
            new CatalogItem.Entity(item.CreatorEntityId, creatorEntityType, creatorEntityType),
            new CatalogItem.Entity(item.CreatorEntityId, creatorEntityType, creatorEntityType),
            item.Data is StaticData.Playfab.Item.RubyData ? false : null, // IsStackable
            item.Data switch
            {
                StaticData.Playfab.Item.BuildplateData => ["android.amazonappstore", "android.googleplay", "b.store", "ios.store", "nx.store", "oculus.store.gearvr", "oculus.store.rift", "uwp.store", "uwp.store.mobile", "xboxone.store", "title.bedrockvanilla", "title.earth"],
                StaticData.Playfab.Item.InventoryItemData => ["android.googleplay", "ios.store", "uwp.store", "title.earth"],
                StaticData.Playfab.Item.RubyData => ["android.googleplay", "ios.store", "uwp.store", "title.bedrockvanilla", "title.earth"],
                StaticData.Playfab.Item.QueryManifestData => ["android.googleplay", "ios.store", "uwp.store", "title.earth"],
                _ => throw new UnreachableException(),
            },
            item.Tags,
            item.CreationDate,
            item.LastModifiedDate,
            item.StartDate,
            item.Contents.Select(content => content switch
            {
                StaticData.Playfab.Item.QueryManifestContent qmContent => new CatalogItem.QueryManifestContent(qmContent.Id, qmContent.Url, qmContent.MaxClientVersion, qmContent.MinClientVersion, qmContent.Tags, qmContent.Type),
                _ => content,
            }),
            item.ThumbnailImageId is null ? [] : [new(item.ThumbnailImageId, "Thumbnail", "Thumbnail", $"/playfab/images/{item.ThumbnailImageId}.png")],
            item.ItemReferences.Select(reference => new CatalogItem.ItemReference(reference.Id, reference.Amount)),
            price,
            price,
            [],
            item.Data switch
            {
                StaticData.Playfab.Item.BuildplateData data => CatalogItem.DisplayPropertiesR.CreateBuildplate(
                    "Minecraft",
                    data.Cost,
                    item.Purchasable,
                    data.Rarity.ToString().ToLowerInvariant(),
                    [new("entitlement_EarthBuildPlate", data.Id, data.Version)],
                    data.Id,
                    data.Size.ToString().ToLowerInvariant(),
                    data.UnlockLevel
                ),
                StaticData.Playfab.Item.InventoryItemData data => CatalogItem.DisplayPropertiesR.CreateInventoryItem(
                    data.Cost,
                    data.Rarity.ToString(),
                    [new("entitlement_InventoryItemOffer", data.Id, data.Version)],
                    data.Id,
                    data.Amount
                ),
                StaticData.Playfab.Item.RubyData data => CatalogItem.DisplayPropertiesR.CreateRuby(
                    data.BonusCoinCount,
                    data.CoinCount,
                    data.OriginalCreatorId,
                    data.Sku
                ),
                StaticData.Playfab.Item.QueryManifestData data => CatalogItem.DisplayPropertiesR.CreateQueryManifest(
                    data.MinClientVersion,
                    data.MaxClientVersion,
                    data.Tabs.Select(tab => new CatalogItem.DisplayPropertiesR.Tab(
                        tab.ScreenLayoutQueries.Select(layoutQuery => new CatalogItem.DisplayPropertiesR.Tab.ScreenLayoutQuery(
                            // TODO: haven't seen it yet, but it's possible these can have properties
                            layoutQuery.ColumnType is Solace.StaticData.Playfab.Tab.ColumnType.Rectangle ? new object() : null,
                            layoutQuery.ColumnType is Solace.StaticData.Playfab.Tab.ColumnType.Square ? new object() : null,
                            layoutQuery.ColumnType is Solace.StaticData.Playfab.Tab.ColumnType.Grid ? new object() : null,
                            layoutQuery.Queries.Select(query => new CatalogItem.DisplayPropertiesR.Tab.ScreenLayoutQuery.Query(
                                query.ProductIds,
                                query.QueryContentTypes.Select(type => type.ToString()),
                                query.TopCount
                            )),
                            layoutQuery.ComponentId
                        )),
                        tab.TabIcon,
                        tab.TabTitle,
                        tab.TabId
                    )),
                    data.GlobalNotSearchQueryTags
                ),
                _ => throw new UnreachableException(),
            }
        );
    }

    private CatalogItem[] CreateItemData(StaticData.StaticDataProvider staticData)
    {
        var someCurrencyId = Guid.Parse("0113e233-7637-48e7-91b0-349fdc74713d");

        var brfPrice = new CatalogItem.PriceR([
            new([new(PlayfabApiUtils.MinecoinCurrencyId, PlayfabApiUtils.MinecoinCurrencyId, PlayfabApiUtils.MinecoinCurrencyId, 0)]),
            new([new(someCurrencyId, someCurrencyId, someCurrencyId, 0)])
        ], []);

        return [
            // required for shop to load for some reason...
            new CatalogItem(
                new("B63A0803D3653643", "namespace", "namespace"),
                new("B63A0803D3653643", "namespace", "namespace"),
                Guid.Parse("230f5996-04b2-4f0e-83e5-4056c7f1d946"),
                "bundle",
                [new("FriendlyId", Guid.Parse("53bee6fe-c9d9-43c9-b3af-4c5438fba4b7"))],
                null,
                new() { ["en-US"] = "Bold Rabbit Feet", ["NEUTRAL"] = "Bold Rabbit Feet", ["neutral"] = "Bold Rabbit Feet", },
                new() { ["en-US"] = "§", ["NEUTRAL"] = "§", ["neutral"] = "§", },
                new() { ["en-US"] = new(["Animal"]), ["NEUTRAL"] = new(["Animal"]), ["neutral"] = new(["Animal"]), },
                "PersonaDurable",
                new("301F442C3B63DC20", "master_player_account", "master_player_account"),
                new("301F442C3B63DC20", "master_player_account", "master_player_account"),
                false, // IsStackable
                ["android.amazonappstore", "android.googleplay",  "b.store",  "ios.store",  "nx.store",  "oculus.store.gearvr", "oculus.store.rift", "uwp.store",  "uwp.store.mobile",  "xboxone.store", "title.bedrockvanilla", "title.earth"],
                ["230f5996-04b2-4f0e-83e5-4056c7f1d946", "4f7cdadd-a33c-489d-8969-752ca689f567", "is_achievement", "earth_achievement", "tag.animal", "1P"],
                new(2020, 12, 7, 22, 46, 33, 066, DateTimeKind.Utc),
                new(2023, 8, 10, 14, 11, 19, 81, DateTimeKind.Utc),
                null,
                [new Dictionary<string,object>() {
                    ["Id"] = "f4a2cf48-45c1-4fda-86d0-9d24c069f0a9",
                    ["Url"] = "https://xforgeassets001.xboxlive.com/pf-title-b63a0803d3653643-20ca2/f4a2cf48-45c1-4fda-86d0-9d24c069f0a9/primary.zip",
                    ["MaxClientVersion"] = "65535.65535.65535",
                    ["MinClientVersion"] = "1.13.0",
                    ["Tags"] = Array.Empty<string>(),
                    ["Type"] = "personabinary",
                }],
                [new("e7314d2a-8097-48f0-b0e8-039084a22049", "Thumbnail", "Thumbnail", "/playfab/images/shoes_bold_striped_rabbit_thumbnail_0.png")],
                [new(Guid.Parse("8eb22e2c-db50-4e30-a3d2-0c355e479e74"), 1)],
                brfPrice,
                brfPrice,
                [],
                CatalogItem.DisplayPropertiesR.CreatePersona(
                    "Minecraft",
                    0,
                    true,
                    "rare",
                    [new("persona_piece", Guid.Parse("4f7cdadd-a33c-489d-8969-752ca689f567"), "1.1.0"),],
                    Guid.Parse("53bee6fe-c9d9-43c9-b3af-4c5438fba4b7"),
                    "persona_feet"
                )
            ),
            .. staticData.Playfab.Items.Select(item => StaticDataItemToCatalogItem(item.Value)),
        ];
    }

    private static IEdmModel CreateCatalogItemEdmModel()
    {
        var builder = new ODataConventionModelBuilder();
        builder.EntitySet<CatalogItem>("CatalogItem");

        builder.ComplexType<CatalogItem.Entity>();
        builder.ComplexType<CatalogItem.AlternateId>();
        builder.ComplexType<CatalogItem.KeywordValues>();
        builder.ComplexType<CatalogItem.Image>();
        builder.ComplexType<CatalogItem.ItemReference>();
        builder.ComplexType<CatalogItem.PriceR>();
        builder.ComplexType<CatalogItem.PriceR.Price>();
        builder.ComplexType<CatalogItem.CurrencyAmount>();
        builder.ComplexType<CatalogItem.DisplayPropertiesR>();
        builder.ComplexType<CatalogItem.DisplayPropertiesR.Tab>();
        builder.ComplexType<CatalogItem.DisplayPropertiesR.Tab.ScreenLayoutQuery>();
        builder.ComplexType<CatalogItem.DisplayPropertiesR.Tab.ScreenLayoutQuery.Query>();

        return builder.GetEdmModel();
    }
}