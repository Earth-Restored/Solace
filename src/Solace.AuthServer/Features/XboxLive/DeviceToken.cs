using Solace.AuthServer.Features.Common;
using Solace.Common.Asp.Auth;

namespace Solace.AuthServer.Features.XboxLive;

public sealed class DeviceToken : AuthToken, ITokenData<DeviceToken>
{
    public required string Did { get; init; }
}