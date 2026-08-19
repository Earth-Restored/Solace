namespace Solace.WebPortal.Common.Features.Store;

public sealed record BuildplateDto(Guid BuildplateId, string? BuildplateName, int Cost, int UnlockLevel, bool Is1Player, IEnumerable<string> Tags, RarityDto Rarity, Version Version);
