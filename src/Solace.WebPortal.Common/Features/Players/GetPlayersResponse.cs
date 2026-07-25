namespace Solace.WebPortal.Common.Features.Players;

public sealed record GetPlayersResponse(
    List<PlayerDto> Players,
    int TotalPlayers,
    int TotalPages,
    int CurrentPage
);
