using System.Text.Json.Serialization;

namespace Solace.AuthServer.Features.Live.Login;

[JsonNamingPolicy(JsonKnownNamingPolicy.CamelCase)]
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
