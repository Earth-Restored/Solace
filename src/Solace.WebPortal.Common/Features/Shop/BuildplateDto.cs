namespace Solace.WebPortal.Common.Features.Shop;

public sealed record BuildplateDto(Guid BuildplateId, string? BuildplateName, int Cost, int UnlockLevel, RarityDto Rarity, Version Version);
