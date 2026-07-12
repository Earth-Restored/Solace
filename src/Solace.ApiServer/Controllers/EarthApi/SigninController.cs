using Asp.Versioning;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using System.Text.RegularExpressions;
using Solace.Common.Utils;
using Solace.Common;
using Solace.DB;
using Solace.ApiServer.Utils;
using Solace.ApiServer.Models;
using Microsoft.AspNetCore.DataProtection;
using Solace.ApiServer.Authentication;

namespace Solace.ApiServer.Controllers;

[ApiVersion("1.1")]
internal sealed partial class SigninController : SolaceControllerBase
{
    private readonly EarthDbContext _earthDb;
    private readonly bool _localLoginOnly;
    private readonly ITimeLimitedDataProtector _protector;
    private readonly CryptoSecrets _cryptoSecrets;
    private readonly ILogger<SigninController> _logger;

    public SigninController(EarthDbContext earthDb, IConfiguration configuration, IDataProtectionProvider dataProtectionProvider, CryptoSecrets cryptoSecrets, ILogger<SigninController> logger)
    {
        _earthDb = earthDb;
        _localLoginOnly = configuration.GetValue<bool>("Authentication:LocalLoginOnly");
        _protector = dataProtectionProvider.CreateProtector(GenoaAuthenticationHandler.DataProtectionPurpose).ToTimeLimitedDataProtector();
        _cryptoSecrets = cryptoSecrets;
        _logger = logger;
    }

    [HttpPost("api/v{version:apiVersion}/player/profile/{profileID}")]
    [HttpPost("1/api/v{version:apiVersion}/player/profile/{profileID}")]
    public async Task<Results<ContentHttpResult, BadRequest>> Post(string profileID, CancellationToken cancellationToken)
    {
        if (profileID != "signin")
        {
            return TypedResults.BadRequest();
        }

        var signinRequest = await Request.Body.AsJsonAsync<SigninRequest>(cancellationToken);

        if (signinRequest is null)
        {
            LogBadSignInRequestWarning("Null json");
            return TypedResults.BadRequest();
        }

        if (signinRequest.SessionTicket.Length < 37)
        {
            LogBadSignInRequestWarning($"Bad request parts ({signinRequest.SessionTicket.Length})");
            return TypedResults.BadRequest();
        }

        const int GuidLength = 36;

        var userIdString = signinRequest.SessionTicket.AsSpan(0, GuidLength);
        var jwt = signinRequest.SessionTicket.AsSpan(GuidLength + 1);

        if (Guid.TryParse(userIdString, out var userId))
        {
            var token = JwtUtils.Verify<Tokens.Shared.PlayfabSessionTicket>(jwt.ToString(), _cryptoSecrets.PlayfabSessionTicketSecret, _logger);

            if (token is null)
            {
                LogBadSignInRequestWarning("Invalid jwt");
                return TypedResults.BadRequest();
            }

            if (token.Expired is true)
            {
                LogBadSignInRequestDebug("Expired jwt");
                return TypedResults.BadRequest();
            }

            if (userId != token.Data.UserId)
            {
                LogBadSignInRequestWarning("Invalid user id does not match token user id");
                return TypedResults.BadRequest();
            }
        }
        else
        {
            if (_localLoginOnly)
            {
                LogMicrosoftLoginDisabled();
                return TypedResults.BadRequest();
            }

            var dashIndex = signinRequest.SessionTicket.IndexOf('-');
            if (dashIndex is -1 || dashIndex == signinRequest.SessionTicket.Length - 1)
            {
                LogBadSignInRequestWarning("Bad parts");
                return TypedResults.BadRequest();
            }

            userIdString = signinRequest.SessionTicket.AsSpan(0, dashIndex);
            // jwt = signinRequest.SessionTicket.AsSpan(dashIndex + 1);

            if (!GetUserIdRegex().IsMatch(userIdString))
            {
                LogBadSignInRequestWarning($"User id not match ({userIdString})");
                return TypedResults.BadRequest();
            }

            userId = IdTranslator.ToGuid(userIdString);

            await _earthDb.EnsureAccountExists(userId);
        }

        // TODO: make the time configurable
        var authToken = _protector.Protect(userId.ToString(), TimeSpan.FromHours(1));

        return EarthJson(new Dictionary<string, object?>()
        {
            ["authenticationToken"] = authToken,
            ["basePath"] = "/1",
            ["clientProperties"] = new object(),
            ["mixedReality"] = null,
            ["mrToken"] = null,
            ["streams"] = null,
            ["tokens"] = new object(),
            ["updates"] = new object(),
        });
    }

    [GeneratedRegex("^[0-9A-F]{15,16}$")]
    private static partial Regex GetUserIdRegex();

    private sealed record SigninRequest(
        double Latitude,
        double Longitude,
        string DeviceId,
        string DeviceOS,
        string DeviceToken,
        string Language,
        string SessionTicket,
        object Streams
    );

    [LoggerMessage(Level = LogLevel.Debug, Message = "Bad sign in request - {Reason}")]
    private partial void LogBadSignInRequestDebug(string Reason);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Bad sign in request - {Reason}")]
    private partial void LogBadSignInRequestWarning(string Reason);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Microsoft login attempt - local login only is enabled")]
    private partial void LogMicrosoftLoginDisabled();
}
