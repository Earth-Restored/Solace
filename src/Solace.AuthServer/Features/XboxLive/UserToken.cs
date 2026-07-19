using Solace.AuthServer.Features.Common;

namespace Solace.AuthServer.Features.XboxLive;

public sealed class UserToken : AuthToken, ITokenData<UserToken>
{
    public required Guid Xid { get; init; }

    public required Guid Uhs { get; init; }

    public required Guid UserId { get; init; }

    public required string Username { get; init; }
}