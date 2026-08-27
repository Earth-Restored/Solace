using System.Text.Json.Serialization;

namespace Solace.ApiServer;

public sealed record class Config(Config.LoginR Login, Config.XboxLiveR XboxLive, Config.PlayfabApiR PlayfabApi)
{
    public static readonly Config Default = new Config
    (
        new LoginR(
            SoapHeaderValidityMinutes: 1,
            UserTokenValidityMinutes: 7 * 24 * 60,
            DeviceTokenValidityMinutes: 1,
            XboxTokenValidityMinutes: 7 * 24 * 60
        ),
        new XboxLiveR(
            TokenValidityMinutes: 7 * 24 * 60
        ),
        new PlayfabApiR(
            EntityTokenValidityMinutes: 24 * 60,
            SessionTicketValidityMinutes: 24 * 60
        )
    );

    public sealed record LoginR(
        int SoapHeaderValidityMinutes,
        int UserTokenValidityMinutes,
        int DeviceTokenValidityMinutes,
        int XboxTokenValidityMinutes
    );

    public sealed record XboxLiveR(
        int TokenValidityMinutes
    );

    public sealed record PlayfabApiR(
        int EntityTokenValidityMinutes,
        int SessionTicketValidityMinutes
    );
}
