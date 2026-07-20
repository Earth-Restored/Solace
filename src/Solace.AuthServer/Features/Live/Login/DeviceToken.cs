using Solace.AuthServer.Features.Common;
using Solace.Common.Asp.Auth;

namespace Solace.AuthServer.Features.Live.Login;

// todo: data
public sealed record DeviceToken()
    : ITokenData<DeviceToken>;