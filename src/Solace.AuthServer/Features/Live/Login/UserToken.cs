using Solace.Common.Asp.Auth;

namespace Solace.AuthServer.Features.Live.Login;

// todo: remove PasswordSalt and PasswordHash, get from db
public sealed record UserToken(
    Guid UserId,
    string Username,
    string PasswordSalt, // base64
    string PasswordHash // base64
) : ITokenData<UserToken>;