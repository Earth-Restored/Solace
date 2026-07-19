namespace Solace.AuthServer.Features.Common;

public sealed record PlayfabXboxToken(
    Guid UserId
) : ITokenData<PlayfabXboxToken>;