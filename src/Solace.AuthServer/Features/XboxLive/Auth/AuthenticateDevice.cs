using System.Text.Json.Serialization;
using Immediate.Apis.Shared;
using Immediate.Handlers.Shared;
using Microsoft.Extensions.Options;
using Solace.AuthServer.Utils;
using Solace.Common.Asp;
using Solace.Common.Asp.Auth;
using Solace.Common.Asp.Json;

namespace Solace.AuthServer.Features.XboxLive.Auth;

[Handler]
[MapPost("device.auth.xboxlive.com/device/authenticate")]
public sealed partial class AuthenticateDevice(
    CryptoSecrets cryptoSecrets,
    IOptions<AuthSettings> authSettingsOption
)
{
    [ForcePascalCase] // [JsonNamingPolicy(JsonKnownNamingPolicy.PascalCase)] does not work currently, throws ArgumentOutOfRangeException does not work currently, throws ArgumentOutOfRangeException
    public sealed record Command(
        Command.PropertiesR Properties,
        string RelyingParty,
        string TokenType
    )
    {
        [ForcePascalCase] // [JsonNamingPolicy(JsonKnownNamingPolicy.PascalCase)] does not work currently, throws ArgumentOutOfRangeException
        public sealed record PropertiesR(
            string AuthMethod,
            string RpsTicket,
            string SiteName
        );
    }

    [ForcePascalCase] // [JsonNamingPolicy(JsonKnownNamingPolicy.PascalCase)] does not work currently, throws ArgumentOutOfRangeException
    public sealed record Response(
        string IssueInstant,
        string NotAfter,
        string Token,
        Dictionary<string, Dictionary<string, string>> DisplayClaims
    );

    private async ValueTask<Response> HandleAsync(
        Command _,
        CancellationToken cancellationToken)
    {
        var tokenValidity = ValidityDatePair.Create(authSettingsOption.Value.TokenValidityMinutes);
        var token = new DeviceToken()
        {
            Did = "F700F376F3793B3A", // TODO: implement
        };

        return new Response(
              tokenValidity.IssuedStr,
              tokenValidity.ExpiresStr,
              JwtUtils.Sign<AuthToken>(token, cryptoSecrets.LiveAuthTokenSecret, tokenValidity),
              new()
              {
                  ["xdi"] = new()
                  {
                      ["did"] = token.Did,
                  },
              }
        );
    }
}