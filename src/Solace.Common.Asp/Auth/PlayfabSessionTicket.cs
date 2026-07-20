namespace Solace.Common.Asp.Auth;

public sealed record PlayfabSessionTicket(
    Guid UserId
) : ITokenData<PlayfabSessionTicket>;