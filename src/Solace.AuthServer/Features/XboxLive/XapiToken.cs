using Solace.Common.Asp.Auth;

namespace Solace.AuthServer.Features.XboxLive;

public sealed record XapiToken(
    Guid UserId,
    string Username
) : ITokenData<XapiToken>;