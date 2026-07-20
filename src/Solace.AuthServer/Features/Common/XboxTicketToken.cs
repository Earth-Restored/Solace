using Solace.Common.Asp.Auth;

namespace Solace.AuthServer.Features.Common;

public sealed record XboxTicketToken(
    Guid UserId,
    string Username
) : ITokenData<XboxTicketToken>;