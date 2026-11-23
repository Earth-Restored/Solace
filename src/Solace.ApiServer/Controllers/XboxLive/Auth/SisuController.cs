using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Solace.ApiServer.Models;
using Solace.ApiServer.Utils;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace Solace.ApiServer.Controllers.XboxLive.Auth;

// Sisu is the sign-in flow used by the iOS XAL client, in place of the device/user/title/xsts calls Android makes.
// authenticate sends the client to the login page, which hands a Live user token back as the OAuth code;
// login.live.com/oauth20_token.srf exchanges it for an Xbox ticket, and authorize turns that into the Xbox tokens.
[Route("sisu.xboxlive.com")]
internal sealed class SisuController : SolaceControllerBase
{
    private static Config Config => Program.config;

    // XAL's JSON parser doesn't decode \u escapes, so & in the redirect URL must be written as-is
    private static readonly JsonSerializerOptions RelaxedJsonOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private readonly CryptoSecrets _cryptoSecrets;

    public SisuController(CryptoSecrets cryptoSecrets)
    {
        _cryptoSecrets = cryptoSecrets;
    }

    internal sealed record AuthenticateRequest(
        string AppId,
        string RedirectUri,
        string DeviceToken,
        string? Sandbox,
        string? TokenType,
        string[]? Offers,
        Dictionary<string, string>? Query
    );

    private sealed record AuthenticateResponse(
        string MsaOauthRedirect,
        Dictionary<string, string> MsaRequestParameters
    );

    internal sealed record AuthorizeRequest(
        string AccessToken,
        string AppId,
        string DeviceToken,
        string? Sandbox,
        string? SiteName,
        string? SessionId,
        bool? UseModernGamertag
    );

    private sealed record TokenResponse<TClaims>(
        string IssueInstant,
        string NotAfter,
        string Token,
        TClaims DisplayClaims
    );

    private sealed record AuthorizeResponse(
        string DeviceToken,
        TokenResponse<Dictionary<string, Dictionary<string, string>>> TitleToken,
        TokenResponse<Dictionary<string, Dictionary<string, string>[]>> UserToken,
        TokenResponse<Dictionary<string, Dictionary<string, string>[]>> AuthorizationToken,
        string WebPage,
        string Sandbox,
        bool UseModernGamertag
    );

    [HttpPost("authenticate")]
    public Results<ContentHttpResult, UnauthorizedHttpResult, BadRequest> Authenticate([FromBody] AuthenticateRequest request)
    {
        if (JwtUtils.Verify<Tokens.Xbox.AuthToken>(request.DeviceToken, _cryptoSecrets.LiveAuthTokenSecret)?.Data is not Tokens.Xbox.DeviceToken)
        {
            return TypedResults.Unauthorized();
        }

        if (!request.RedirectUri.StartsWith("ms-xal-", StringComparison.OrdinalIgnoreCase))
        {
            return TypedResults.BadRequest();
        }

        var query = new Dictionary<string, string?>();
        foreach (var (key, value) in request.Query ?? [])
        {
            query[key] = value;
        }

        query["client_id"] = request.AppId;
        query["redirect_uri"] = request.RedirectUri;
        query["response_type"] = "code";

        // The page lives under ppsecure/ so its relative ./login and ./register posts reach LoginController
        string redirect = $"{Request.Scheme}://{Request.Host}/login.live.com/ppsecure/oauth20_authorize.srf{QueryString.Create(query)}";

        Response.Headers["X-SessionId"] = Guid.NewGuid().ToString("N");

        return RelaxedJson(new AuthenticateResponse(redirect, []));
    }

    [HttpPost("authorize")]
    public Results<ContentHttpResult, UnauthorizedHttpResult> Authorize([FromBody] AuthorizeRequest request)
    {
        if (JwtUtils.Verify<Tokens.Xbox.AuthToken>(request.DeviceToken, _cryptoSecrets.LiveAuthTokenSecret)?.Data is not Tokens.Xbox.DeviceToken)
        {
            return TypedResults.Unauthorized();
        }

        string accessToken = request.AccessToken.StartsWith("t=", StringComparison.Ordinal) ? request.AccessToken[2..] : request.AccessToken;
        var ticket = JwtUtils.Verify<Tokens.Shared.XboxTicketToken>(accessToken, _cryptoSecrets.LoginXboxTokenSecret)?.Data;

        if (ticket is null)
        {
            return TypedResults.Unauthorized();
        }

        var tokenValidity = ValidityDatePair.Create(Config.XboxLive.TokenValidityMinutes);

        var titleToken = new Tokens.Xbox.TitleToken()
        {
            Tid = GetTitleId(request.AppId),
        };

        var userToken = new Tokens.Xbox.UserToken()
        {
            Xid = ticket.UserId,
            Uhs = ticket.UserId,

            UserId = ticket.UserId,
            Username = ticket.Username,
        };

        var xapiToken = new Tokens.Xbox.XapiToken(userToken.UserId, userToken.Username);

        return RelaxedJson(new AuthorizeResponse(
            request.DeviceToken,
            new(
                tokenValidity.IssuedStr,
                tokenValidity.ExpiresStr,
                JwtUtils.Sign<Tokens.Xbox.AuthToken>(titleToken, _cryptoSecrets.LiveAuthTokenSecret, tokenValidity),
                new()
                {
                    ["xti"] = new()
                    {
                        ["tid"] = titleToken.Tid,
                    },
                }
            ),
            new(
                tokenValidity.IssuedStr,
                tokenValidity.ExpiresStr,
                JwtUtils.Sign<Tokens.Xbox.AuthToken>(userToken, _cryptoSecrets.LiveAuthTokenSecret, tokenValidity),
                new()
                {
                    ["xui"] = [
                        new()
                        {
                            ["uhs"] = userToken.Uhs.ToString(),
                        },
                    ],
                }
            ),
            // XSTS token for http://xboxlive.com, same claims as XstsController
            new(
                tokenValidity.IssuedStr,
                tokenValidity.ExpiresStr,
                JwtUtils.Sign(xapiToken, _cryptoSecrets.LiveXapiTokenSecret, tokenValidity),
                new()
                {
                    ["xui"] = [
                        new()
                        {
                            ["xid"] = XuidUtils.ToNumericString(userToken.Xid),
                            ["uhs"] = userToken.Uhs.ToString(),

                            ["gtg"] = userToken.Username,
                            ["agg"] = "Adult",

                            ["usr"] = "185 190 234",
                            ["prv"] = "184 186 187 188 191 193 195 196 198 199 200 201 203 204 205 206 208 211 217 220 224 227 228 235 238 245 247 249 252 254 255"
                        },
                    ],
                }
            ),
            $"{Request.Scheme}://{Request.Host}/sisu.xboxlive.com/client",
            request.Sandbox ?? "RETAIL",
            request.UseModernGamertag ?? true
        ));
    }

    // XAL opens the authorize WebPage (Xbox's welcome/gamertag page); with no status in the redirect it assumes success
    [HttpGet("client")]
    public Results<RedirectHttpResult, BadRequest> Client([FromQuery] string? redirect, [FromQuery] string? state)
    {
        if (redirect is null || !redirect.StartsWith("ms-xal-", StringComparison.OrdinalIgnoreCase))
        {
            return TypedResults.BadRequest();
        }

        return TypedResults.Redirect(state is null ? redirect : $"{redirect}?state={Uri.EscapeDataString(state)}");
    }

    // XAL rejects the authorize response unless the title id matches its own config (xboxservices.config TitleId)
    private static string GetTitleId(string appId)
        => appId.ToLowerInvariant() switch
        {
            "0000000040281e53" => "1712756451", // Minecraft Earth iOS
            _ => "2037747551", // Minecraft Earth Android, as in TitleController
        };

    private static ContentHttpResult RelaxedJson(object value)
        => TypedResults.Content(JsonSerializer.Serialize(value, RelaxedJsonOptions), "application/json");
}
