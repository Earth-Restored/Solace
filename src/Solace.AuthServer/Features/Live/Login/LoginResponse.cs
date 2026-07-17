namespace Solace.AuthServer.Features.Live.Login;

public sealed record LoginResponse(
    Guid UserId,
    string Username,
    string FirstName,
    string LastName,
    string Token,
    string TokenIssuedAt,
    string TokenExpires,
    string SessionKey
);
