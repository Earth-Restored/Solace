using System.Diagnostics;
using Immediate.Apis.Shared;
using Immediate.Handlers.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Solace.Db.Earth;
using Solace.Db.Playfab;
using Solace.Db.Playfab.Models.Items;
using Solace.EventBus.Client;
using Solace.StaticData;
using Solace.WebPortal.Common;
using Solace.WebPortal.Common.Features.Store;

namespace Solace.WebPortal.Features.Store.Items;

[Handler]
[MapPut("items/{ItemId}")]
[MapGroup<StoreGroup>]
[Authorize(Policy = Permissions.EditRoles)]
public static partial class UpdateItem
{
    public sealed record Command([property: FromRoute] Guid ItemId, [property: FromBody] ItemDto Item);

    private static async ValueTask<Results<Ok, NotFound, BadRequest>> HandleAsync(
        Command command,
        PlayfabDbContext playfabDb,
        EarthDbContext earthDb,
        EventBusClient eventBus,
        StaticDataProvider staticData,
        CancellationToken cancellationToken
    )
    {
        if (!ItemDtoUtils.IsValid(command.Item))
        {
            return TypedResults.BadRequest();
        }

        var now = DateTimeOffset.UtcNow;

        var id = command.ItemId;

        await using var transaction = await playfabDb.Database.BeginTransactionAsync(cancellationToken);

        var item = await playfabDb.Items
            .Include(item => item.Data)
            .AsTracking()
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

        if (item is null)
        {
            return TypedResults.NotFound();
        }

        item.Purchasable = command.Item.Purchasable;
        item.Title = command.Item.Title.Trim();
        item.Description = command.Item.Description.Trim();
        item.LastModifiedDate = now;
        item.StartDate = command.Item.StartDate;
        item.TitleTranslations = command.Item.TitleTranslations.ToDictionary(k => k.Key, v => v.Value.Trim(), StringComparer.Ordinal);
        item.DescriptionTranslations = command.Item.DescriptionTranslations.ToDictionary(k => k.Key, v => v.Value.Trim(), StringComparer.Ordinal);
        item.ItemReferences = [new ItemReferenceEF()
        {
            Id = Guid.NewGuid(), // not used by game, might have been used for db id? but we don't care about that
            Amount = command.Item.ItemDataType switch
            {
                ItemDataTypeDto.Buildplate => 1,
                ItemDataTypeDto.InventoryItem => command.Item.InventoryItemData!.Amount,
                _ => throw new UnreachableException(),
            }
        }];

        ItemDtoUtils.CreateTags(item.Tags, id, command.Item, staticData.Catalog.ItemsCatalog);

        switch (command.Item.ItemDataType)
        {
            case ItemDataTypeDto.Buildplate:
                {
                    if (item.Data is not BuildplateDataEF bpDataEF)
                    {
                        if (item.Data is not null)
                        {
                            playfabDb.ItemData.Remove(item.Data);
                            await playfabDb.SaveChangesAsync(cancellationToken);
                        }

                        bpDataEF = new BuildplateDataEF()
                        {
#pragma warning disable CS0618 // Type or member is obsolete
                            Id = id,
#pragma warning restore CS0618 // Type or member is obsolete
                        };

                        item.Data = bpDataEF;
                    }

                    var bpData = command.Item.BuildplateData;
                    Debug.Assert(bpData is not null);

                    var template = await earthDb.TemplateBuildplates
                        .AsNoTracking()
                        .Select(template => new { template.Id, template.Size, })
                        .FirstOrDefaultAsync(template => template.Id == bpData.BuildplateId, cancellationToken);

                    var size = template is null
                        ? BuildplateSizeEF.Medium
                        : template.Size switch
                        {
                            8 => BuildplateSizeEF.Small,
                            16 => BuildplateSizeEF.Medium,
                            32 => BuildplateSizeEF.Large,
                            _ => BuildplateSizeEF.Medium,
                        };

                    bpDataEF.BuildplateId = bpData.BuildplateId;
                    bpDataEF.Cost = bpData.Cost;
                    bpDataEF.Size = size;
                    bpDataEF.UnlockLevel = bpData.UnlockLevel;
                    bpDataEF.Rarity = ItemDtoUtils.MapRarity(bpData.Rarity);
                    bpDataEF.Version = bpData.Version;
                }

                break;
            case ItemDataTypeDto.InventoryItem:
                {
                    if (item.Data is not InventoryItemDataEF invDataEF)
                    {
                        if (item.Data is not null)
                        {
                            playfabDb.ItemData.Remove(item.Data);
                            await playfabDb.SaveChangesAsync(cancellationToken);
                        }

                        invDataEF = new InventoryItemDataEF()
                        {
#pragma warning disable CS0618 // Type or member is obsolete
                            Id = id,
#pragma warning restore CS0618 // Type or member is obsolete
                        };

                        item.Data = invDataEF;
                    }

                    var invData = command.Item.InventoryItemData;
                    Debug.Assert(invData is not null);

                    invDataEF.ItemId = invData.ItemId;
                    invDataEF.Cost = invData.Cost;
                    invDataEF.Amount = invData.Amount;
                    invDataEF.Rarity = ItemDtoUtils.MapRarity(invData.Rarity);
                    invDataEF.Version = invData.Version;
                }

                break;
            default:
                throw new UnreachableException();
        }

        await playfabDb.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        await eventBus.PublishAsync("playfab", "shop_data_updated", "", cancellationToken);

        return TypedResults.Ok();
    }
}
