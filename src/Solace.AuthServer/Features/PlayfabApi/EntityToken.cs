using Solace.Common.Asp.Auth;

namespace Solace.AuthServer.Features.PlayfabApi;

public sealed record EntityToken(
    Guid Id,
    string Type
) : ITokenData<EntityToken>;
