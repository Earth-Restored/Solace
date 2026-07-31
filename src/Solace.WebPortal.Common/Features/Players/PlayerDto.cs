namespace Solace.WebPortal.Common.Features.Players;

public sealed record PlayerDto(
    Guid Id,
    string? OwnerUser,
    bool OwnsProfile,
    string? Username,
    int Health,
    int MaxHealth,
    int Level,
    int TotalExperience,
    float? LevelProgressPercentage, // 0 to 1; null - max level already
    int PurchasedRubies,
    int EarnedRubies,
    int? BuildplateCount
);
