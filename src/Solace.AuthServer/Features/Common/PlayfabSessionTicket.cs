namespace Solace.AuthServer.Features.Common;

public sealed record PlayfabSessionTicket(
    Guid UserId
) : ITokenData<PlayfabSessionTicket>;