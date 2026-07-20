using Solace.Common.Asp.Json;

namespace Solace.AuthServer.Features.PlayfabApi;

[ForcePascalCase]
public sealed record ResponseEntity(
    Guid Id,
    string Type,
    string TypeString
);