using System.Collections.Frozen;
using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.OData.Edm;
using Microsoft.OData.ModelBuilder;
using OData2Linq;
using Solace.Db.Playfab;
using Solace.Db.Playfab.Models.Items;
using Solace.Db.Playfab.Models.Tabs;
using Solace.StaticData;

namespace Solace.AuthServer.Features.PlayfabApi.Catalog;

public sealed class CatalogService : IDisposable
{
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(30);

    private readonly IDbContextFactory<PlayfabDbContext> _playfabDbFactory;
    private readonly StaticData.Playfab _staticData;
    private readonly IEdmModel _edmModel;
    private readonly SemaphoreSlim _lock = new(1, 1);

    private CatalogItem[]? _itemData;
    private FrozenDictionary<Guid, CatalogItem>? _itemById;
    private DateTimeOffset _cacheExpiration = DateTimeOffset.MinValue;

    public CatalogService(IDbContextFactory<PlayfabDbContext> playfabDbFactory, StaticDataProvider staticData)
    {
        _playfabDbFactory = playfabDbFactory;
        _staticData = staticData.Playfab;
        _edmModel = CreateCatalogItemEdmModel();
    }

    public void ClearCache()
    {
        lock (_lock)
        {
            _itemData = null;
            _itemById = null;
            _cacheExpiration = DateTimeOffset.MinValue;
        }
    }

    private async Task<(CatalogItem[] ItemData, FrozenDictionary<Guid, CatalogItem> ItemById)> GetCachedData(CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;

        if (_itemData is not null && _itemById is not null && _cacheExpiration > now)
        {
            return (_itemData, _itemById);
        }

        await _lock.WaitAsync(cancellationToken);

        try
        {
            if (_itemData is not null && _itemById is not null && _cacheExpiration > now)
            {
                return (_itemData, _itemById);
            }

            await using var playfabDb = await _playfabDbFactory.CreateDbContextAsync(cancellationToken);
            var itemData = await CreateItemData(playfabDb, _staticData, cancellationToken);
            var itemById = itemData.ToFrozenDictionary(item => item.Id);

            _itemData = itemData;
            _itemById = itemById;
            _cacheExpiration = now.Add(CacheDuration);

            return (itemData, itemById);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<ODataQuery<CatalogItem>> CreateItemsQueryAsync(CancellationToken cancellationToken = default)
    {
        var (itemData, _) = await GetCachedData(cancellationToken);

        return itemData
            .AsQueryable()
            .OData(settings =>
            {
                settings.EnableCaseInsensitive = true;
                settings.ValidationSettings.MaxNodeCount = 10000;
            }, _edmModel);
    }

    public async Task<CatalogItem?> TryGetItemAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var (_, itemById) = await GetCachedData(cancellationToken);
        return itemById.TryGetValue(id, out var catalogItem) ? catalogItem : null;
    }

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

    public CatalogItem ItemEFToCatalogItem(ItemEF item)
    {
        var price = item.Data switch
        {
            BuildplateDataEF data => new CatalogItem.PriceR([
                new([
                    new(PlayfabApiUtils.RubyCurrencyId, PlayfabApiUtils.RubyCurrencyId, PlayfabApiUtils.RubyCurrencyId, data.Cost),
                ]),
            ], []),
            InventoryItemDataEF data => new CatalogItem.PriceR([
                new([
                    new(PlayfabApiUtils.RubyCurrencyId, PlayfabApiUtils.RubyCurrencyId, PlayfabApiUtils.RubyCurrencyId, data.Cost),
                ]),
            ], []),
            _ => throw new UnreachableException(),
        };

        const string creatorEntityType = "title_player_account";

        return new CatalogItem(
            new CatalogItem.Entity(item.SourceEntityId, "namespace", "namespace"),
            new CatalogItem.Entity(item.SourceEntityId, "namespace", "namespace"),
            item.Id,
            item.Data switch
            {
                BuildplateDataEF => "bundle",
                InventoryItemDataEF => "bundle",
                _ => throw new UnreachableException(),
            },
            item.FriendlyId is null ? [] : [new("FriendlyId", item.FriendlyId.Value)],
            item.FriendlyId,
            ((IEnumerable<KeyValuePair<string, string>>)[new("NEUTRAL", item.Title), .. item.TitleTranslations, new("neutral", item.Title)])
                .ToDictionary(StringComparer.Ordinal),
            ((IEnumerable<KeyValuePair<string, string>>)[new("NEUTRAL", item.Description), .. item.DescriptionTranslations, new("neutral", item.Description)])
                .ToDictionary(StringComparer.Ordinal),
            item.Keywords.ToDictionary(item => item.Key, item => new CatalogItem.KeywordValues(item.Value.Values), StringComparer.Ordinal),
            item.Data switch
            {
                BuildplateDataEF => "BuildplateOffer",
                InventoryItemDataEF => "InventoryItemOffer",
                _ => throw new UnreachableException(),
            },
            new CatalogItem.Entity(item.CreatorEntityId, creatorEntityType, creatorEntityType),
            new CatalogItem.Entity(item.CreatorEntityId, creatorEntityType, creatorEntityType),
            null, // IsStackable
            item.Data switch
            {
                BuildplateDataEF => ["android.amazonappstore", "android.googleplay", "b.store", "ios.store", "nx.store", "oculus.store.gearvr", "oculus.store.rift", "uwp.store", "uwp.store.mobile", "xboxone.store", "title.bedrockvanilla", "title.earth"],
                InventoryItemDataEF => ["android.googleplay", "ios.store", "uwp.store", "title.earth"],
                _ => throw new UnreachableException(),
            },
            item.Tags,
            item.CreationDate.UtcDateTime,
            item.LastModifiedDate.UtcDateTime,
            item.StartDate.UtcDateTime,
            [],
            item.ThumbnailImageId is null ? [] : [new(item.ThumbnailImageId.Value, "Thumbnail", "Thumbnail", $"/playfab/images/{item.ThumbnailImageId}.png")],
            item.ItemReferences.Select(reference => new CatalogItem.ItemReference(reference.Id, reference.Amount)),
            price,
            price,
            [],
            item.Data switch
            {
                BuildplateDataEF data => CatalogItem.DisplayPropertiesR.CreateBuildplate(
                    "Minecraft",
                    data.Cost,
                    item.Purchasable,
                    data.Rarity.ToString().ToLowerInvariant(),
                    [new("entitlement_EarthBuildPlate", data.Id, data.Version)],
                    data.BuildplateId,
                    data.Size.ToString().ToLowerInvariant(),
                    data.UnlockLevel
                ),
                InventoryItemDataEF data => CatalogItem.DisplayPropertiesR.CreateInventoryItem(
                    data.Cost,
                    data.Rarity.ToString(),
                    [new("entitlement_InventoryItemOffer", data.Id, data.Version)],
                    data.ItemId,
                    data.Amount
                ),
                _ => throw new UnreachableException(),
            }
        );
    }

    public void Dispose()
        => _lock.Dispose();

    private async Task<CatalogItem[]> CreateItemData(PlayfabDbContext playfabDb, StaticData.Playfab staticData, CancellationToken cancellationToken = default)
    {
        var someCurrencyId = Guid.Parse("0113e233-7637-48e7-91b0-349fdc74713d");

        var brfPrice = new CatalogItem.PriceR([
            new([new(PlayfabApiUtils.MinecoinCurrencyId, PlayfabApiUtils.MinecoinCurrencyId, PlayfabApiUtils.MinecoinCurrencyId, 0)]),
            new([new(someCurrencyId, someCurrencyId, someCurrencyId, 0)])
        ], []);

        List<CatalogItem> result = [];

        result.Add(
            new CatalogItem(
                new("B63A0803D3653643", "namespace", "namespace"),
                new("B63A0803D3653643", "namespace", "namespace"),
                Guid.Parse("230f5996-04b2-4f0e-83e5-4056c7f1d946"),
                "bundle",
                [new("FriendlyId", Guid.Parse("53bee6fe-c9d9-43c9-b3af-4c5438fba4b7"))],
                null,
                new(StringComparer.Ordinal) { ["en-US"] = "Bold Rabbit Feet", ["NEUTRAL"] = "Bold Rabbit Feet", ["neutral"] = "Bold Rabbit Feet", },
                new(StringComparer.Ordinal) { ["en-US"] = "§", ["NEUTRAL"] = "§", ["neutral"] = "§", },
                new(StringComparer.Ordinal) { ["en-US"] = new(["Animal"]), ["NEUTRAL"] = new(["Animal"]), ["neutral"] = new(["Animal"]), },
                "PersonaDurable",
                new("301F442C3B63DC20", "master_player_account", "master_player_account"),
                new("301F442C3B63DC20", "master_player_account", "master_player_account"),
                false, // IsStackable
                ["android.amazonappstore", "android.googleplay", "b.store", "ios.store", "nx.store", "oculus.store.gearvr", "oculus.store.rift", "uwp.store", "uwp.store.mobile", "xboxone.store", "title.bedrockvanilla", "title.earth"],
                ["230f5996-04b2-4f0e-83e5-4056c7f1d946", "4f7cdadd-a33c-489d-8969-752ca689f567", "is_achievement", "earth_achievement", "tag.animal", "1P"],
                new(2020, 12, 7, 22, 46, 33, 066, DateTimeKind.Utc),
                new(2023, 8, 10, 14, 11, 19, 81, DateTimeKind.Utc),
                null,
                [new Dictionary<string, object>(StringComparer.Ordinal) {
                    ["Id"] = "f4a2cf48-45c1-4fda-86d0-9d24c069f0a9",
                    ["Url"] = "https://xforgeassets001.xboxlive.com/pf-title-b63a0803d3653643-20ca2/f4a2cf48-45c1-4fda-86d0-9d24c069f0a9/primary.zip",
                    ["MaxClientVersion"] = "65535.65535.65535",
                    ["MinClientVersion"] = "1.13.0",
                    ["Tags"] = Array.Empty<string>(),
                    ["Type"] = "personabinary",
                }],
                [new(Guid.Parse("e7314d2a-8097-48f0-b0e8-039084a22049"), "Thumbnail", "Thumbnail", "/playfab/images/shoes_bold_striped_rabbit_thumbnail_0.png")],
                [new(Guid.Parse("8eb22e2c-db50-4e30-a3d2-0c355e479e74"), 1)],
                brfPrice,
                brfPrice,
                [],
                CatalogItem.DisplayPropertiesR.CreatePersona(
                    "Minecraft",
                    0,
                    true,
                    "rare",
                    [new("persona_piece", Guid.Parse("4f7cdadd-a33c-489d-8969-752ca689f567"), new Version(1, 1, 0)),],
                    Guid.Parse("53bee6fe-c9d9-43c9-b3af-4c5438fba4b7"),
                    "persona_feet"
                )
            )
        );

        const string creatorEntityType = "title_player_account";

        var tabsData = await playfabDb.Tabs
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        result.Add(new CatalogItem(
            new CatalogItem.Entity("B63A0803D3653643", "namespace", "namespace"),
            new CatalogItem.Entity("B63A0803D3653643", "namespace", "namespace"),
            Guid.Parse("06e44b91-e7f5-46b6-9986-ca755890f3bf"),
            "catalogItem",
            [],
            null,
            ((IEnumerable<KeyValuePair<string, string>>)[new("NEUTRAL", "Home L1"), .. new Dictionary<string, string>(StringComparer.Ordinal) { ["en-US"] = "Home L1" }, new("neutral", "Home L1")])
                .ToDictionary(StringComparer.Ordinal),
            ((IEnumerable<KeyValuePair<string, string>>)[new("NEUTRAL", "Home L1"), .. new Dictionary<string, string>(StringComparer.Ordinal) { ["en-US"] = "Home L1" }, new("neutral", "Home L1")])
                .ToDictionary(StringComparer.Ordinal),
            new Dictionary<string, CatalogItem.KeywordValues>(StringComparer.Ordinal) { ["en-US"] = new([]), ["NEUTRAL"] = new([]), ["neutral"] = new([]), },
            "GenoaQueryManifest_V0.0.3",
            new CatalogItem.Entity("3C0BE9326354CBB7", creatorEntityType, creatorEntityType),
            new CatalogItem.Entity("3C0BE9326354CBB7", creatorEntityType, creatorEntityType),
            null, // IsStackable
            ["android.googleplay", "ios.store", "uwp.store", "title.earth"],
            ["mctestdefault"],
            new(2020, 12, 10, 18, 59, 39, 396, DateTimeKind.Utc),
            new(2021, 1, 4, 19, 42, 53, 773, DateTimeKind.Utc),
            new(2021, 1, 5, 17, 0, 0, DateTimeKind.Utc),
            [new CatalogItem.QueryManifestContent(Guid.Parse("f3f2b4fc-f144-4357-9e41-198db3a47957"), "/playfab/master_loc_contents.json", new Version(6555, 6555, 6555), new Version(1, 2, 0), [], "resourcebinary")],
            [],
            [],
            null,
            null,
            [],
            CatalogItem.DisplayPropertiesR.CreateQueryManifest(
                new Version(0, 25, 0),
                new Version(1, 0, 20),
                tabsData
                    .Select(tab => new CatalogItem.DisplayPropertiesR.Tab(
                    tab.ScreenLayoutQueries.Select(layoutQuery => new CatalogItem.DisplayPropertiesR.Tab.ScreenLayoutQuery(
                        // TODO: haven't seen it yet, but it's possible these can have properties
                        layoutQuery.ColumnType is ColumnTypeEF.Rectangle ? new object() : null,
                        layoutQuery.ColumnType is ColumnTypeEF.Square ? new object() : null,
                        layoutQuery.ColumnType is ColumnTypeEF.Grid ? new object() : null,
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
                staticData.StoreNotSearchQueryTags
            )
        ));

        await foreach (var item in playfabDb.Items
            .AsNoTracking()
            .Include(item => item.Data)
            .AsAsyncEnumerable()
            .WithCancellation(cancellationToken))
        {
            result.Add(ItemEFToCatalogItem(item));
        }

        return [.. result];
    }

    private static IEdmModel CreateCatalogItemEdmModel()
    {
        var builder = new ODataConventionModelBuilder();

        builder.EnableLowerCamelCase();

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