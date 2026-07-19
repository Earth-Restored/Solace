using System.Text.Json.Serialization;

namespace Solace.AuthServer.Features.XboxLive.Privacy;

[JsonNamingPolicy(JsonKnownNamingPolicy.CamelCase)]
public sealed record PeopleResponse(
    object[] Users
);