using Asp.Versioning;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Serilog;
using System.Text.RegularExpressions;
using Solace.Common.Utils;
using Solace.Common;
using Solace.DB;
using Solace.ApiServer.Utils;
using Solace.ApiServer.Models;

namespace Solace.ApiServer.Controllers;

[ApiVersion("1.1")]
internal sealed partial class SigninController : SolaceControllerBase
{
    private readonly EarthDbContext _earthDb;

    public SigninController(EarthDbContext earthDb)
    {
        _earthDb = earthDb;
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
            Log.Warning($"Sign in - request null");
            return TypedResults.BadRequest();
        }

        if (signinRequest.SessionTicket.Length < 37)
        {
            Log.Warning($"Sign in - request parts bad ({signinRequest.SessionTicket.Length})");
            return TypedResults.BadRequest();
        }

        const int GuidLength = 36;

        var userIdString = signinRequest.SessionTicket.AsSpan(0, GuidLength);
        var jwt = signinRequest.SessionTicket.AsSpan(GuidLength + 1);

        if (Guid.TryParse(userIdString, out var userId))
        {
            var token = JwtUtils.Verify<Tokens.Shared.PlayfabSessionTicket>(jwt.ToString(), Program.config.PlayfabApi.SessionTicketSecretBytes);

            if (token is null)
            {
                Log.Warning($"Sign in - invalid jwt");
                return TypedResults.BadRequest();
            }

            if (token.Expired is true)
            {
                Log.Warning($"Sign in - expired jwt");
                return TypedResults.BadRequest();
            }

            if (userId != token.Data.UserId)
            {
                Log.Warning($"Sign in - user id does not match token user id");
                return TypedResults.BadRequest();
            }
        }
        else
        {
            if (!GetUserIdRegex().IsMatch(userIdString))
            {
                Log.Warning($"Sign in - user id not match ({userIdString})");
                return TypedResults.BadRequest();
            }

            userId = IdTranslator.ToGuid(userIdString);

            await _earthDb.EnsureAccountExists(userId);

            // microsoft login - cannot validate
        }

        // TODO: generate secure session token
        string authToken = userId.ToString();

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
}
