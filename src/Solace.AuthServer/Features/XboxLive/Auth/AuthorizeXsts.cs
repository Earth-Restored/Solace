using Immediate.Apis.Shared;
using Immediate.Handlers.Shared;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Options;
using Solace.AuthServer.Features.Common;
using Solace.Common.Asp.Auth;
using Solace.Common.Asp.Json;

namespace Solace.AuthServer.Features.XboxLive.Auth;

[Handler]
[MapPost("xsts.auth.xboxlive.com/xsts/authorize")]
public sealed partial class AuthorizeXsts(
    CryptoSecrets cryptoSecrets,
    IOptions<AuthSettings> authSettingsOption,
    ILogger<AuthorizeXsts> logger
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
            string SandboxId,
            string DeviceToken,
            string TitleToken,
            string[] UserTokens
        );
    }

    [ForcePascalCase] // [JsonNamingPolicy(JsonKnownNamingPolicy.PascalCase)] does not work currently, throws ArgumentOutOfRangeException
    public sealed record Response(
        string IssueInstant,
        string NotAfter,
        string Token,
        Dictionary<string, Dictionary<string, string>[]> DisplayClaims
    );

    private async ValueTask<Results<Ok<Response>, UnauthorizedHttpResult, BadRequest>> HandleAsync(
       Command command,
       CancellationToken cancellationToken)
    {
        var authSettings = authSettingsOption.Value;

        if (command.Properties.UserTokens.Length is not 1)
        {
            return TypedResults.BadRequest();
        }

        var deviceTokenAuth = JwtUtils.Verify<AuthToken>(command.Properties.DeviceToken, cryptoSecrets.LiveAuthTokenSecret, logger)?.Data;
        var titleTokenAuth = JwtUtils.Verify<AuthToken>(command.Properties.TitleToken, cryptoSecrets.LiveAuthTokenSecret, logger)?.Data;
        var userTokenAuth = JwtUtils.Verify<AuthToken>(command.Properties.UserTokens[0], cryptoSecrets.LiveAuthTokenSecret, logger)?.Data;

        if (deviceTokenAuth is not DeviceToken || titleTokenAuth is not TitleToken || userTokenAuth is not UserToken userToken)
        {
            return TypedResults.Unauthorized();
        }

        switch (command.RelyingParty)
        {
            case "http://xboxlive.com":
                {
                    var tokenValidity = ValidityDatePair.Create(authSettings.TokenValidityMinutes);
                    var token = new XapiToken(userToken.UserId, userToken.Username);

                    return TypedResults.Ok(new Response(
                        tokenValidity.IssuedStr,
                        tokenValidity.ExpiresStr,
                        JwtUtils.Sign(token, cryptoSecrets.LiveXapiTokenSecret, tokenValidity),
                        new()
                        {
                            ["xui"] = [
                                new()
                                {
                                    ["xid"] = userToken.Xid.ToString(),
                                    ["uhs"] = userToken.Uhs.ToString(),

                                    ["gtg"] = userToken.Username,
                                    ["agg"] = "Adult",

                                    ["usr"] = "185 190 234",
                                    ["prv"] = "184 186 187 188 191 193 195 196 198 199 200 201 203 204 205 206 208 211 217 220 224 227 228 235 238 245 247 249 252 254 255"
                                },
                            ]
                        }
                    ));
                }

            case "http://events.xboxlive.com":
                {
                    var tokenValidity = ValidityDatePair.Create(authSettings.TokenValidityMinutes);
                    var token = new XapiToken(userToken.UserId, userToken.Username);

                    return TypedResults.Ok(new Response(
                       tokenValidity.IssuedStr,
                       tokenValidity.ExpiresStr,
                       JwtUtils.Sign(token, cryptoSecrets.LiveXapiTokenSecret, tokenValidity),
                       new()
                       {
                           ["xui"] = [
                                new()
                                {
                                    ["uhs"] = userToken.Uhs.ToString(),
                                },
                           ]
                       }
                   ));
                }

            case "https://b980a380.minecraft.playfabapi.com/":
                {
                    var tokenValidity = ValidityDatePair.Create(authSettings.TokenValidityMinutes);
                    var token = new PlayfabXboxToken(userToken.UserId);

                    return TypedResults.Ok(new Response(
                       tokenValidity.IssuedStr,
                       tokenValidity.ExpiresStr,
                       JwtUtils.Sign(token, cryptoSecrets.LivePlayfabTokenSecret, tokenValidity),
                       new()
                       {
                           ["xui"] = [
                                new()
                                {
                                    ["uhs"] = userToken.Uhs.ToString(),
                                },
                           ]
                       }
                   ));
                }

            default:
                return TypedResults.BadRequest();
        }
    }
}