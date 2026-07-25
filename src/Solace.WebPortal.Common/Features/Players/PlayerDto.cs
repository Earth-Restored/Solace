namespace Solace.WebPortal.Common.Features.Players;

public sealed record PlayerDto(
    Guid Id,
    string? Username,
    int Health,
    int MaxHealth,
    int Level,
    float? LevelProgressPercentage, // 0 to 1; null - max level already
    int PurchasedRubies,
    int EarnedRubies
);
