namespace Solace.WebPortal.Common.Features.Players;

public sealed record SkinDto(
    byte[] SkinData,
    bool IsSlim
);
