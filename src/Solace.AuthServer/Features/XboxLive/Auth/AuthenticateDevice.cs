using Immediate.Apis.Shared;
using Immediate.Handlers.Shared;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Options;
using Solace.AuthServer.Features.Common;
using Solace.Common.Asp.Auth;
using Solace.Common.Asp.Json;

namespace Solace.AuthServer.Features.XboxLive.Auth;

[Handler]
[MapPost("device.auth.xboxlive.com/device/authenticate")]
public sealed partial class AuthenticateDevice(
    CryptoSecrets cryptoSecrets,
    ILogger<AuthenticateDevice> logger,
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

    private async ValueTask<Results<Ok<Response>, UnauthorizedHttpResult>> HandleAsync(
        Command command,
        CancellationToken cancellationToken)
    {
        var ticket = JwtUtils.Verify<XboxTicketToken>(command.Properties.RpsTicket, cryptoSecrets.LoginXboxTokenSecret, logger)?.Data;

        if (ticket is null)
        {
            return TypedResults.Unauthorized();
        }

        var tokenValidity = ValidityDatePair.Create(authSettingsOption.Value.TokenValidityMinutes);
        var token = new DeviceToken()
        {
            Did = "F700F376F3793B3A", // TODO: implement
        };

        return TypedResults.Ok(new Response(
              tokenValidity.IssuedStr,
              tokenValidity.ExpiresStr,
              JwtUtils.Sign<AuthToken>(token, cryptoSecrets.LiveAuthTokenSecret, tokenValidity),
              new(StringComparer.Ordinal)
              {
                  ["xdi"] = new(StringComparer.Ordinal)
                  {
                      ["did"] = token.Did,
                  },
              }
        ));
    }
}