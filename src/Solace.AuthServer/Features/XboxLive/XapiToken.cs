using Solace.AuthServer.Features.Common;

namespace Solace.AuthServer.Features.XboxLive;

public sealed record XapiToken(
    Guid UserId,
    string Username
) : ITokenData<XapiToken>;