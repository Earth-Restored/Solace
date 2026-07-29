using Solace.Common.Asp.Auth;

namespace Solace.AuthServer.Features.Live.Login;

public sealed record UserToken(
    Guid ProfileId,
    string Username
) : ITokenData<UserToken>;