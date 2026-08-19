namespace Solace.WebPortal.Common.Features.Store;

public sealed record InventoryItemDto(Guid ItemId, string? ItemName, int Cost, int Amount, RarityDto Rarity, Version Version);
