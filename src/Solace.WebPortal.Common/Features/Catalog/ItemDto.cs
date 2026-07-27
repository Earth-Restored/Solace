namespace Solace.WebPortal.Common.Features.Catalog;

public sealed record ItemDto(
    Guid Id,
    string Name,
    int Aux,
    bool Stackable,
    ItemDtoType Type,
    ItemDtoCategory Category,
    ItemDtoRarity Rarity,
    ItemDtoUseType UseType,
    ItemDtoBlockInfo? BlockInfo,
    ItemDtoToolInfo? ToolInfo,
    ItemDtoConsumeInfo? ConsumeInfo,
    ItemDtoFuelInfo? FuelInfo,
    ItemDtoBoostInfo? BoostInfo,
    ItemDtoExperience Experience
);
