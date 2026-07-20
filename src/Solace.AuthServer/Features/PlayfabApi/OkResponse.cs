using System.Text.Json.Serialization;

namespace Solace.AuthServer.Features.PlayfabApi;

[JsonNamingPolicy(JsonKnownNamingPolicy.CamelCase)]
public sealed record OkResponse<T>(
    int Code,
    string Status,
    T Data
);
