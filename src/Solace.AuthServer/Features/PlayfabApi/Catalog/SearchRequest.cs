using System.Text.Json.Serialization;

namespace Solace.AuthServer.Features.PlayfabApi.Catalog;

[JsonNamingPolicy(JsonKnownNamingPolicy.CamelCase)]
public sealed record SearchRequest(
    bool Count,
    string Filter,
    string? Select,
    string? OrderBy,
    int? Top,
    int? Skip,
    string Scid
);