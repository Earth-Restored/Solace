using Solace.AuthServer.Features.Common;

namespace Solace.AuthServer.Features.Live.Login;

public sealed record DeviceToken()
    : ITokenData<DeviceToken>;