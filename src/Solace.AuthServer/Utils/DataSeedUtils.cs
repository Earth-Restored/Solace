using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Solace.Db.Playfab;
using Solace.Db.Playfab.Models.Items;
using Solace.Db.Playfab.Models.Tabs;
using Solace.StaticData;

namespace Solace.AuthServer.Utils;

internal static class DataSeedUtils
{
    public static async Task SeedPlayfabDataAsync(PlayfabDbContext playfabDb, Playfab staticData, bool update, bool force, CancellationToken cancellationToken = default)
    {
        var staticDataVersion = staticData.Version;

        await using var transaction = await playfabDb.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            var seedHistory = await playfabDb.SeedingHistory.FirstOrDefaultAsync(history => history.Key == "PlayfabData", cancellationToken);

            var isFirstTime = seedHistory is null;
            var hasNewVersion = seedHistory is not null && staticData.Version > seedHistory.Version;

            var shouldSeed = force || isFirstTime || (update && hasNewVersion);

            if (!shouldSeed)
            {
                return;
            }

            await playfabDb.Tabs.ExecuteDeleteAsync(cancellationToken);
            await playfabDb.Items.ExecuteDeleteAsync(cancellationToken);

            await InternalSeedPlayfabDataAsync(playfabDb, staticData, cancellationToken);

            if (seedHistory is null)
            {
                seedHistory = new Db.Playfab.Models.SeedingHistory()
                {
                    Key = "PlayfabData",
                    SeededAt = DateTimeOffset.UtcNow,
                    Version = staticDataVersion,
                };

                playfabDb.SeedingHistory.Add(seedHistory);
            }
            else
            {
                seedHistory.SeededAt = DateTimeOffset.UtcNow;
                seedHistory.Version = staticDataVersion;
            }

            await playfabDb.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private static async Task InternalSeedPlayfabDataAsync(PlayfabDbContext playfabDb, Playfab staticData, CancellationToken cancellationToken)
    {
        foreach (var (itemId, item) in staticData.Items)
        {
            ItemDataEF data = item.Data switch
            {
                Playfab.Item.BuildplateData buildplateData => new BuildplateDataEF()
                {
                    Id = itemId,
                    BuildplateId = buildplateData.Id,
                    Cost = buildplateData.Cost,
                    Size = buildplateData.Size switch
                    {
                        Playfab.Item.BuidplateSize.Small => BuildplateSizeEF.Small,
                        Playfab.Item.BuidplateSize.Medium => BuildplateSizeEF.Medium,
                        Playfab.Item.BuidplateSize.Large => BuildplateSizeEF.Large,
                        _ => throw new UnreachableException(),
                    },
                    UnlockLevel = buildplateData.UnlockLevel,
                    Rarity = ConvertRarity(buildplateData.Rarity),
                    Version = buildplateData.Version,
                },
                Playfab.Item.InventoryItemData inventoryData => new InventoryItemDataEF()
                {
                    Id = itemId,
                    ItemId = inventoryData.Id,
                    Cost = inventoryData.Cost,
                    Amount = inventoryData.Amount,
                    Rarity = ConvertRarity(inventoryData.Rarity),
                    Version = inventoryData.Version,
                },
                Playfab.Item.QueryManifestData queryData => new QueryManifestDataEF()
                {
                    MinClientVersion = queryData.MinClientVersion,
                    MaxClientVersion = queryData.MaxClientVersion,
                    Tabs = [.. SeedTabs(playfabDb, staticData)],
                    GlobalNotSearchQueryTags = [.. queryData.GlobalNotSearchQueryTags],
                },
                _ => throw new UnreachableException(),
            };

            var itemEF = new ItemEF()
            {
                Id = itemId,
                FriendlyId = item.FriendlyId,
                Purchasable = item.Purchasable,
                Title = item.Title,
                Description = item.Description,
                ThumbnailImageId = item.ThumbnailImageId,
                CreationDate = new DateTimeOffset(item.CreationDate, TimeSpan.Zero),
                LastModifiedDate = new DateTimeOffset(item.LastModifiedDate, TimeSpan.Zero),
                StartDate = new DateTimeOffset(item.StartDate, TimeSpan.Zero),
                SourceEntityId = item.SourceEntityId,
                CreatorEntityId = item.CreatorEntityId,
                Data = data,
                Tags = [.. item.Tags],
                Keywords = item.Keywords.ToDictionary(
                    item => item.Key, item => new KeywordValuesEF()
                    {
                        Values = [.. item.Value.Values],
                    }, StringComparer.Ordinal),
                TitleTranslations = item.TitleTranslations.ToDictionary(StringComparer.Ordinal),
                DescriptionTranslations = item.DescriptionTranslations.ToDictionary(StringComparer.Ordinal),
                ItemReferences = [.. item.ItemReferences.Select(itemRef =>
                    new ItemReferenceEF(){
                        Id = itemRef.Id,
                        Amount = itemRef.Amount,
                    })]
            };

            data.Item = itemEF;
        }

        await playfabDb.SaveChangesAsync(cancellationToken);

        static IEnumerable<TabEF> SeedTabs(PlayfabDbContext playfabDb, Playfab staticData)
        {
            for (var i = 0; i < staticData.ShopTabs.Length; i++)
            {
                var tab = staticData.ShopTabs[i];

                var tabEF = new TabEF()
                {
                    TabIndex = i,
                    TabId = tab.TabId,
                    TabTitle = tab.TabTitle,
                    TabIcon = tab.TabIcon,
                    ScreenLayoutQueries = [.. tab.ScreenLayoutQueries.Select(sq => new ScreenLayoutQueryEF()
                    {
                        ColumnType = sq.ColumnType switch{
                            Playfab.Tab.ColumnType.Rectangle => ColumnTypeEF.Rectangle,
                            Playfab.Tab.ColumnType.Square => ColumnTypeEF.Square,
                            Playfab.Tab.ColumnType.Grid => ColumnTypeEF.Grid,
                            _ => throw new UnreachableException(),
                        },
                        ComponentId = sq.ComponentId,
                        Queries = [.. sq.Queries.Select(q => new QueryEF() {
                            TopCount = q.TopCount,
                            ProductIds = [..q.ProductIds.Select(Guid.Parse)],
                            QueryContentTypes = [..q.QueryContentTypes.Select(ct => ct switch
                            {
                                Playfab.ContentType.Durable => ContentTypeEF.Durable,
                                Playfab.ContentType.Collection => ContentTypeEF.Collection,
                                Playfab.ContentType.Bundle => ContentTypeEF.Bundle,
                                Playfab.ContentType.Persona => ContentTypeEF.Persona,
                                Playfab.ContentType.Genoa => ContentTypeEF.Genoa,
                                Playfab.ContentType.BuildplateOffer => ContentTypeEF.BuildplateOffer,
                                Playfab.ContentType.RubyOffer => ContentTypeEF.RubyOffer,
                                Playfab.ContentType.InventoryItemOffer => ContentTypeEF.InventoryItemOffer,
                                _ => throw new UnreachableException(),
                            })]
                        })],
                    })],
                };

                playfabDb.Tabs.Add(tabEF);

                yield return tabEF;
            }
        }

        static RarityEF ConvertRarity(Playfab.Item.Rarity rarity)
        {
            return rarity switch
            {
                Playfab.Item.Rarity.None => RarityEF.None,
                Playfab.Item.Rarity.Common => RarityEF.Common,
                Playfab.Item.Rarity.Uncommon => RarityEF.Uncommon,
                Playfab.Item.Rarity.Rare => RarityEF.Rare,
                Playfab.Item.Rarity.Epic => RarityEF.Epic,
                Playfab.Item.Rarity.Legendary => RarityEF.Legendary,
                _ => throw new UnreachableException(),
            };
        }
    }
}
