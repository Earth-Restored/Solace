using Solace.AuthServer.Features.Common;

namespace Solace.AuthServer.Features.PlayfabApi;

public sealed record EntityToken(
    Guid Id,
    string Type
) : ITokenData<EntityToken>;
