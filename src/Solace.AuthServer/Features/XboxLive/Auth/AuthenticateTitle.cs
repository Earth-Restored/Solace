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
[MapPost("title.auth.xboxlive.com/title/authenticate")]
public sealed partial class AuthenticateTitle(
    CryptoSecrets cryptoSecrets,
    IOptions<AuthSettings> authSettingsOption
)
{
    [ForcePascalCase] // [JsonNamingPolicy(JsonKnownNamingPolicy.PascalCase)] does not work currently, throws ArgumentOutOfRangeException
    public sealed record Command(
        Command.PropertiesR Properties,
        string RelyingParty,
        string TokenType
    )
    {
        [ForcePascalCase] // [JsonNamingPolicy(JsonKnownNamingPolicy.PascalCase)] does not work currently, throws ArgumentOutOfRangeException
        public sealed record PropertiesR(
            string AuthMethod,
            string DeviceToken,
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
        var token = new TitleToken()
        {
            Tid = "2037747551",
        };

        return new Response(
            tokenValidity.IssuedStr,
            tokenValidity.ExpiresStr,
            JwtUtils.Sign<AuthToken>(token, cryptoSecrets.LiveAuthTokenSecret, tokenValidity),
            new()
            {
                ["xdi"] = new()
                {
                    ["tid"] = token.Tid,
                },
            }
        );
    }
}