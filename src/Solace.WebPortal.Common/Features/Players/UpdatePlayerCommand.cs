namespace Solace.WebPortal.Common.Features.Players;

public sealed record UpdatePlayerCommand(
    string? Username = null,
    int? Health = null,
    int? PurchasedRubies = null,
    int? EarnedRubies = null
);
