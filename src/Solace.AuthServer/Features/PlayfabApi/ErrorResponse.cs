using System.Text.Json.Serialization;

namespace Solace.AuthServer.Features.PlayfabApi;

[JsonNamingPolicy(JsonKnownNamingPolicy.CamelCase)]
public sealed record ErrorResponse(
    int Code,
    string Status,
    string Error,
    int ErrorCode,
    string ErrorMessage,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] Dictionary<string, string[]>? ErrorDetails
);