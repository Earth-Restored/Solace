namespace Solace.AuthServer.Features.Live.Login;

public sealed class AuthSettings
{
    public int SoapHeaderValidityMinutes { get; init; }

    public int DeviceTokenValidityMinutes { get; init; }

    public int UserTokenValidityMinutes { get; init; }

    public int XboxTokenValidityMinutes { get; init; }
}
