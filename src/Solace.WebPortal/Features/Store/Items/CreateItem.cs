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
using Solace.StaticData;
using Solace.WebPortal.Common;
using Solace.WebPortal.Common.Features.Store;

namespace Solace.WebPortal.Features.Store.Items;

[Handler]
[MapPost("items/")]
[MapGroup<StoreGroup>]
[Authorize(Policy = Permissions.EditRoles)]
public static partial class CreateItem
{
    public sealed record Command([property: FromBody] ItemDto Item);

    private static async ValueTask<Results<Ok<Guid>, BadRequest>> HandleAsync(
        Command command,
        PlayfabDbContext playfabDb,
        EarthDbContext earthDb,
        StaticDataProvider staticData,
        CancellationToken cancellationToken
    )
    {
        if (!ItemDtoUtils.IsValid(command.Item))
        {
            return TypedResults.BadRequest();
        }

        var now = DateTimeOffset.UtcNow;

        var id = Guid.CreateVersion7();

        var item = new ItemEF()
        {
            Id = id,
            FriendlyId = null,
            Purchasable = command.Item.Purchasable,
            Title = command.Item.Title.Trim(),
            Description = command.Item.Description.Trim(),
            ThumbnailImageId = null,
            CreationDate = now,
            LastModifiedDate = now,
            StartDate = command.Item.StartDate,
            SourceEntityId = "B63A0803D3653643",
            CreatorEntityId = "3B1B443CE3FD8EBA",
            TitleTranslations = command.Item.TitleTranslations.ToDictionary(k => k.Key, v => v.Value.Trim(), StringComparer.Ordinal),
            DescriptionTranslations = command.Item.DescriptionTranslations.ToDictionary(k => k.Key, v => v.Value.Trim(), StringComparer.Ordinal),
            ItemReferences = [new ItemReferenceEF()
            {
                Id = Guid.NewGuid(), // not used by game, might have been used for db id? but we don't care about that
                Amount = command.Item.ItemDataType switch
                {
                    ItemDataTypeDto.Buildplate => 1,
                    ItemDataTypeDto.InventoryItem => command.Item.InventoryItemData!.Amount,
                    _ => throw new UnreachableException(),
                }
            }],
        };

        ItemDtoUtils.CreateTags(item.Tags, id, command.Item, staticData.Catalog.ItemsCatalog);

        switch (command.Item.ItemDataType)
        {
            case ItemDataTypeDto.Buildplate:
                {
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

                    item.Data = new BuildplateDataEF()
                    {
#pragma warning disable CS0618 // Type or member is obsolete
                        Id = id,
#pragma warning restore CS0618 // Type or member is obsolete
                        BuildplateId = bpData.BuildplateId,
                        Cost = bpData.Cost,
                        Size = size,
                        UnlockLevel = bpData.UnlockLevel,
                        Rarity = ItemDtoUtils.MapRarity(bpData.Rarity),
                        Version = bpData.Version,
                    };
                }

                break;
            case ItemDataTypeDto.InventoryItem:
                {
                    var invData = command.Item.InventoryItemData;
                    Debug.Assert(invData is not null);

                    item.Data = new InventoryItemDataEF()
                    {
#pragma warning disable CS0618 // Type or member is obsolete
                        Id = id,
#pragma warning restore CS0618 // Type or member is obsolete
                        ItemId = invData.ItemId,
                        Cost = invData.Cost,
                        Amount = invData.Amount,
                        Rarity = ItemDtoUtils.MapRarity(invData.Rarity),
                        Version = invData.Version,
                    };
                }

                break;
            default:
                throw new UnreachableException();
        }

        playfabDb.Items.Add(item);
        await playfabDb.SaveChangesAsync(cancellationToken);

        return TypedResults.Ok(id);
    }
}
