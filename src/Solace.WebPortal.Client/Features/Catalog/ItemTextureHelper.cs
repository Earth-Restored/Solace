using Solace.WebPortal.Common.Features.Catalog;

namespace Solace.WebPortal.Client.Features.Catalog;

public static class ItemTextureHelper
{
    public const string MissingTextureUrl = "/static/missing_texture.png";

    public static string GetTextureName(ItemDto item)
    {
        if (item.Category is ItemDtoCategory.BoostAdventureXP
            or ItemDtoCategory.BoostCrafting
            or ItemDtoCategory.BoostDefense
            or ItemDtoCategory.BoostEating
            or ItemDtoCategory.BoostHealth
            or ItemDtoCategory.BoostHoarding
            or ItemDtoCategory.BoostItemXP
            or ItemDtoCategory.BoostMiningSpeed
            or ItemDtoCategory.BoostRetention
            or ItemDtoCategory.BoostSmelting
            or ItemDtoCategory.BoostStrength
            or ItemDtoCategory.BoostTappableRadius)
        {
            return item.Id.ToString();
        }

        return $"{item.Name}_{item.Aux}";
    }

    public static string GetTextureUrl(ItemDto item)
        => $"/static/genoa-textures/ui/items/{Uri.EscapeDataString(GetTextureName(item))}.png";
}
