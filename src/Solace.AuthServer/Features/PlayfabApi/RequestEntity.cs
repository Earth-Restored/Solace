using Solace.Common.Asp.Json;

namespace Solace.AuthServer.Features.PlayfabApi;

[ForcePascalCase]
public sealed record RequestEntity(
    Guid? Id,
    string Type
);