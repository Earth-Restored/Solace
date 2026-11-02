using System.ComponentModel;
using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Solace.Db.Earth;
using Solace.Db.Earth.Models.Challenges;
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

            // update should only touch default data
            if (isFirstTime || force)
            {
                await playfabDb.Tabs.ExecuteDeleteAsync(cancellationToken);
                await playfabDb.Items.ExecuteDeleteAsync(cancellationToken);
                await playfabDb.ItemData.ExecuteDeleteAsync(cancellationToken);
            }

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
            if (item.Data is Playfab.Item.QueryManifestData)
            {
                continue;
            }

            var itemEF = await playfabDb.Items
                .AsTracking()
                .Include(item => item.Data)
                .FirstOrDefaultAsync(item => item.Id == itemId, cancellationToken);

            if (itemEF is null)
            {
                itemEF = new ItemEF()
                {
                    Id = itemId,
                    Data = item.Data switch
                    {
                        Playfab.Item.BuildplateData => new BuildplateDataEF()
                        {
#pragma warning disable CS0618 // Type or member is obsolete
                            Id = itemId
#pragma warning restore CS0618 // Type or member is obsolete
                        },
                        Playfab.Item.InventoryItemData => new InventoryItemDataEF()
                        {
#pragma warning disable CS0618 // Type or member is obsolete
                            Id = itemId,
#pragma warning restore CS0618 // Type or member is obsolete
                        },
                        _ => throw new UnreachableException(),
                    }
                };

                itemEF.Data.Item = itemEF;

                playfabDb.Items.Add(itemEF);
            }
            else if (itemEF.Data is null || (itemEF.Data is BuildplateDataEF) != (item.Data is Playfab.Item.BuildplateData))
            {
                if (itemEF.Data is not null)
                {
                    playfabDb.ItemData.Remove(itemEF.Data);
                    await playfabDb.SaveChangesAsync(cancellationToken);
                }

                itemEF.Data = item.Data switch
                {
                    Playfab.Item.BuildplateData => new BuildplateDataEF()
                    {
#pragma warning disable CS0618 // Type or member is obsolete
                        Id = itemId
#pragma warning restore CS0618 // Type or member is obsolete
                    },
                    Playfab.Item.InventoryItemData => new InventoryItemDataEF()
                    {
#pragma warning disable CS0618 // Type or member is obsolete
                        Id = itemId,
#pragma warning restore CS0618 // Type or member is obsolete
                    },
                    _ => throw new UnreachableException(),
                };

                itemEF.Data.Item = itemEF;
            }

            itemEF.FriendlyId = item.FriendlyId;
            itemEF.Purchasable = item.Purchasable;
            itemEF.Title = item.Title;
            itemEF.Description = item.Description;
            itemEF.ThumbnailImageId = item.ThumbnailImageId;
            itemEF.CreationDate = new DateTimeOffset(item.CreationDate, TimeSpan.Zero);
            itemEF.LastModifiedDate = new DateTimeOffset(item.LastModifiedDate, TimeSpan.Zero);
            itemEF.StartDate = new DateTimeOffset(item.StartDate, TimeSpan.Zero);
            itemEF.SourceEntityId = item.SourceEntityId;
            itemEF.CreatorEntityId = item.CreatorEntityId;
            itemEF.Tags = [.. item.Tags];
            itemEF.Keywords = item.Keywords.ToDictionary(
                item => item.Key, item => new KeywordValuesEF()
                {
                    Values = [.. item.Value.Values],
                }, StringComparer.Ordinal);
            itemEF.TitleTranslations = item.TitleTranslations.ToDictionary(StringComparer.Ordinal);
            itemEF.DescriptionTranslations = item.DescriptionTranslations.ToDictionary(StringComparer.Ordinal);
            itemEF.ItemReferences = [.. item.ItemReferences.Select(itemRef =>
                new ItemReferenceEF(){
                    Id = itemRef.Id,
                    Amount = itemRef.Amount,
                })];

            switch (item.Data)
            {
                case Playfab.Item.BuildplateData buildplateData:
                    {
                        var data = (BuildplateDataEF)itemEF.Data!;

                        data.BuildplateId = buildplateData.Id;
                        data.Cost = buildplateData.Cost;
                        data.Size = buildplateData.Size switch
                        {
                            Playfab.Item.BuildplateSize.Small => BuildplateSizeEF.Small,
                            Playfab.Item.BuildplateSize.Medium => BuildplateSizeEF.Medium,
                            Playfab.Item.BuildplateSize.Large => BuildplateSizeEF.Large,
                            _ => throw new UnreachableException(),
                        };
                        data.UnlockLevel = buildplateData.UnlockLevel;
                        data.Rarity = ConvertRarity(buildplateData.Rarity);
                        data.Version = buildplateData.Version;
                    }

                    break;
                case Playfab.Item.InventoryItemData inventoryData:
                    {
                        var data = (InventoryItemDataEF)itemEF.Data!;

                        data.ItemId = inventoryData.Id;
                        data.Cost = inventoryData.Cost;
                        data.Amount = inventoryData.Amount;
                        data.Rarity = ConvertRarity(inventoryData.Rarity);
                        data.Version = inventoryData.Version;
                    }

                    break;
                default:
                    throw new NotImplementedException();
            }

            await playfabDb.SaveChangesAsync(cancellationToken);
        }

        for (var i = 0; i < staticData.StoreTabs.Length; i++)
        {
            var tab = staticData.StoreTabs[i];

            var tabEF = new TabEF()
            {
                TabIndex = i + 1,
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

            await playfabDb.SaveChangesAsync(cancellationToken);
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

    public static async Task SeedSeasonsDataAsync(EarthDbContext earthDb, SeasonsCatalog staticData, bool update, bool force, CancellationToken cancellationToken = default)
    {
        var staticVersion = staticData.Version;
        await using var transaction = await earthDb.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            var seedHistory = await earthDb.SeedingHistory.FirstOrDefaultAsync(history => history.Key == "SeasonsData", cancellationToken);
            var isFirstTime = seedHistory is null;
            var hasNewVersion = seedHistory is not null && staticVersion > seedHistory.Version;

            if (!force && !isFirstTime && (!update || !hasNewVersion))
            {
                return;
            }

            if (isFirstTime || force)
            {
                await earthDb.Challenges.ExecuteDeleteAsync(cancellationToken);
                await earthDb.Seasons.ExecuteDeleteAsync(cancellationToken);
            }

            foreach (var (_, staticSeason) in staticData.Seasons)
            {
                var seasonEF = await earthDb.Seasons.Include(s => s.Challenges).FirstOrDefaultAsync(s => s.Id == staticSeason.Id, cancellationToken);
                if (seasonEF is null)
                {
                    seasonEF = new SeasonEF
                    {
                        Id = staticSeason.Id,
                        Key = staticSeason.Key,
                        SeasonNumber = staticSeason.SeasonNumber,
                        StartDate = staticSeason.StartDate,
                        EndDate = staticSeason.EndDate,
                        IsActive = staticSeason.IsActive,
                        ExclusiveRewards = staticSeason.ExclusiveRewards is not null ? ConvertRewards(staticSeason.ExclusiveRewards) : null,
                    };

                    earthDb.Seasons.Add(seasonEF);
                }
                else
                {
                    seasonEF.StartDate = staticSeason.StartDate;
                    seasonEF.EndDate = staticSeason.EndDate;
                    seasonEF.IsActive = staticSeason.IsActive;
                    seasonEF.ExclusiveRewards = staticSeason.ExclusiveRewards is not null ? ConvertRewards(staticSeason.ExclusiveRewards) : null;
                }

                foreach (var staticChallenge in staticSeason.Challenges)
                {
                    var challengeEF = seasonEF.Challenges.FirstOrDefault(challenge => challenge.Id == staticChallenge.Id);
                    var rewards = ConvertRewards(staticChallenge.Rewards);

                    if (challengeEF is null)
                    {
                        challengeEF = new ChallengeDefinitionEF
                        {
                            Id = staticChallenge.Id,
                            SeasonId = seasonEF.Id,
                            ParentId = staticChallenge.ParentId,
                            PrerequisiteIds = [.. staticChallenge.PrerequisiteIds],
                            PrerequisiteLogicalCondition = MapLogicalCondition(staticChallenge.PrerequisiteLogicalCondition),
                            Order = staticChallenge.Order,
                            TotalThreshold = staticChallenge.TotalThreshold,
                            ObjectiveType = MapObjectiveType(staticChallenge.ObjectiveType),
                            ObjectiveTarget = staticChallenge.ObjectiveTarget,
                            IsFinalSeasonChallenge = staticChallenge.IsFinalSeasonChallenge,
                            Rewards = rewards,
                        };

                        earthDb.Challenges.Add(challengeEF);
                    }
                    else
                    {
                        challengeEF.ParentId = staticChallenge.ParentId;
                        challengeEF.PrerequisiteIds = [.. staticChallenge.PrerequisiteIds];
                        challengeEF.PrerequisiteLogicalCondition = MapLogicalCondition(staticChallenge.PrerequisiteLogicalCondition);
                        challengeEF.Order = staticChallenge.Order;
                        challengeEF.TotalThreshold = staticChallenge.TotalThreshold;
                        challengeEF.ObjectiveType = MapObjectiveType(staticChallenge.ObjectiveType);
                        challengeEF.ObjectiveTarget = staticChallenge.ObjectiveTarget;
                        challengeEF.IsFinalSeasonChallenge = staticChallenge.IsFinalSeasonChallenge;
                        challengeEF.Rewards = rewards;
                    }
                }
            }

            if (seedHistory is null)
            {
                earthDb.SeedingHistory.Add(new Db.Earth.Models.SeedingHistory
                {
                    Key = "SeasonsData",
                    SeededAt = DateTimeOffset.UtcNow,
                    Version = staticVersion,
                });
            }
            else
            {
                seedHistory.SeededAt = DateTimeOffset.UtcNow;
                seedHistory.Version = staticVersion;
            }

            await earthDb.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }

        static Db.Earth.Models.Common.Rewards ConvertRewards(SeasonsCatalog.Rewards rewards)
        {
            var items = new Dictionary<Guid, int>();
            foreach (var item in rewards.Inventory)
            {
                items[item.Id] = items.GetValueOrDefault(item.Id, 0) + item.Amount;
            }

            return new Db.Earth.Models.Common.Rewards(
                rewards.Rubies ?? 0,
                rewards.ExperiencePoints ?? 0,
                rewards.Level,
                items,
                [.. rewards.Buildplates],
                [.. rewards.Challenges.Select(c => c.Id)],
                [.. rewards.PersonaItems]
            );
        }

        static LogicalConditionEF MapLogicalCondition(LogicalCondition condition)
        {
            return condition switch
            {
                LogicalCondition.And => LogicalConditionEF.And,
                LogicalCondition.Or => LogicalConditionEF.Or,
                _ => throw new InvalidEnumArgumentException(nameof(condition), (int)condition, typeof(LogicalCondition)),
            };
        }

        static ObjectiveTypeEF MapObjectiveType(ObjectiveType type)
        {
            return type switch
            {
                ObjectiveType.TappableCollected => ObjectiveTypeEF.TappableCollected,
                ObjectiveType.CraftItem => ObjectiveTypeEF.CraftItem,
                ObjectiveType.SmeltItem => ObjectiveTypeEF.SmeltItem,
                ObjectiveType.AdventurePlayed => ObjectiveTypeEF.AdventurePlayed,
                ObjectiveType.DefeatMob => ObjectiveTypeEF.DefeatMob,
                ObjectiveType.SeasonCapstone => ObjectiveTypeEF.SeasonCapstone,
                ObjectiveType.PlaceBlock => ObjectiveTypeEF.PlaceBlock,
                _ => throw new InvalidEnumArgumentException(nameof(type), (int)type, typeof(ObjectiveType)),
            };
        }
    }
}
