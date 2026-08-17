namespace Solace.WebPortal.Common.Features.Store;

public sealed record BuildplateDto(Guid BuildplateId, string? BuildplateName, int Cost, int UnlockLevel, RarityDto Rarity, Version Version);
