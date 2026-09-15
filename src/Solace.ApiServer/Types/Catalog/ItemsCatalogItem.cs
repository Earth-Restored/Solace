using Solace.ApiServer.Types.Common;
using static Solace.ApiServer.Types.Catalog.ItemsCatalogItem;

namespace Solace.ApiServer.Types.Catalog;

internal sealed record ItemsCatalogItem(
    Guid Id,
    ItemsCatalogItemData Item,
    string Category,
    Rarity Rarity,
    int FragmentsRequired,
    bool Stacks,
    BurnRate? BurnRate,
    ReturnItem[] FuelReturnItems,
    ReturnItem[] ConsumeReturnItems,
    int? Experience,
    Dictionary<string, int?> ExperiencePoints,
    bool Deprecated
)
{
    internal sealed record ReturnItem(
        Guid Id,
        int Amount
    );
};
