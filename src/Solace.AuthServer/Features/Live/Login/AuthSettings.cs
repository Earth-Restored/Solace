namespace Solace.AuthServer.Features.Live.Login;

public sealed class AuthSettings
{
    public required int SoapHeaderValidityMinutes { get; init; }

    public required int DeviceTokenValidityMinutes { get; init; }

    public required int UserTokenValidityMinutes { get; init; }

    public required int XboxTokenValidityMinutes { get; init; }
}
