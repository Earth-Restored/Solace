using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;
using Solace.ApiServer.Models;
using Solace.ApiServer.Models.Playfab;
using Solace.ApiServer.Utils;
using Solace.Common.Utils;
using Solace.DB;

namespace Solace.ApiServer.Controllers.PlayfabApi;

[Route("Client")]
[Route("20CA2.playfabapi.com/Client")]
internal sealed partial class LoginController : SolaceControllerBase
{
    private readonly EarthDbContext _dbContext;
    private readonly CryptoSecrets _cryptoSecrets;

    private readonly int _playfabApiSessionTicketValidityMinutes;
    private readonly int _playfabApiEntityTokenValidityMinutes;

    private readonly ILogger<LoginController> _logger;

    public LoginController(EarthDbContext context, CryptoSecrets cryptoSecrets, IConfiguration configuration, ILogger<LoginController> logger)
    {
        _dbContext = context;
        _cryptoSecrets = cryptoSecrets;

        _playfabApiSessionTicketValidityMinutes = configuration.GetValue<int>("Authentication:PlayfabApi:SessionTicketValidityMinutes");
        _playfabApiEntityTokenValidityMinutes = configuration.GetValue<int>("Authentication:PlayfabApi:EntityTokenValidityMinutes");

        _logger = logger;
    }

    internal sealed record LoginWithCustomIDRequest(
        string TitleId,
        object? EncryptedRequest,
        object? PlayerSecret,
        bool CreateAccount,
        string CustomId
    );

    [HttpPost("LoginWithCustomID")]
    public async Task<Results<ContentHttpResult, BadRequest>> LoginWithCustomID()
    {
        var cancellationToken = Request.HttpContext.RequestAborted;

        var request = await Request.Body.AsJsonAsync(AppJsonContext.Default.LoginWithCustomIDRequest, cancellationToken);

        if (request is null || !GetTitleIdRegex().IsMatch(request.TitleId))
        {
            return TypedResults.BadRequest();
        }

        return JsonCamelCase(new PlayfabErrorResponse(
            403,
            "Forbidden",
            "NotAuthorizedByTitle",
            1191,
            "Action not authorized by title",
            null
        ));
    }

    internal sealed record LoginWithXboxRequest(
        string TitleId,
        object? EncryptedRequest,
        object? PlayerSecret,
        bool CreateAccount,
        string XboxToken
    );

    [HttpPost("LoginWithXbox")]
    public async Task<Results<ContentHttpResult, ForbidHttpResult, NotFound, BadRequest>> LoginWithXbox()
    {
        var cancellationToken = Request.HttpContext.RequestAborted;

        var request = await Request.Body.AsJsonAsync(AppJsonContext.Default.LoginWithXboxRequest, cancellationToken);

        if (request is null || !GetTitleIdRegex().IsMatch(request.TitleId))
        {
            return TypedResults.BadRequest();
        }

        var authorization = XboxAuthorizationUtils.Parse(request.XboxToken);

        if (authorization is not { } authValue)
        {
            return TypedResults.BadRequest();
        }

        var xboxToken = JwtUtils.Verify<Tokens.Shared.PlayfabXboxToken>(authValue.TokenString, _cryptoSecrets.LivePlayfabTokenSecret, _logger);

        if (xboxToken is null || xboxToken.Data.UserId != authValue.UserId)
        {
            // TODO: probably supposed to use a "fake 403" as with LoginWithCustomID
            return TypedResults.Forbid();
        }

        var userId = xboxToken.Data.UserId;

        var account = await _dbContext.Accounts
            .Select(account => new { account.Id, account.CreatedDate, })
            .FirstOrDefaultAsync(account => account.Id == userId, cancellationToken);

        if (account is null)
        {
            return TypedResults.NotFound();
        }

        var sessionTicketValidity = ValidityDatePair.Create(_playfabApiSessionTicketValidityMinutes);
        var sessionTicket = new Tokens.Shared.PlayfabSessionTicket(userId);
        var sessionTicketString = JwtUtils.Sign(sessionTicket, _cryptoSecrets.PlayfabSessionTicketSecret, sessionTicketValidity);

        var entityTokenValidity = ValidityDatePair.Create(_playfabApiEntityTokenValidityMinutes);
        var entityToken = new Tokens.Playfab.EntityToken(userId, "title_player_account");
        var entityTokenString = JwtUtils.Sign(entityToken, _cryptoSecrets.PlayfabEntityTokenSecret, entityTokenValidity);

        return JsonPascalCase(new PlayfabOkResponse(
            200,
            "OK",
            new Dictionary<string, object>()
            {
                ["SessionTicket"] = $"{userId.ToString().ToUpperInvariant()}-{sessionTicketString}",
                ["PlayFabId"] = userId,
                ["NewlyCreated"] = false,
                ["SettingsForUser"] = new Dictionary<string, bool>()
                {
                    ["NeedsAttribution"] = false,
                    ["GatherDeviceInfo"] = true,
                    ["GatherFocusInfo"] = true,
                },
                ["LastLoginTime"] = account.CreatedDate.UtcDateTime,
                ["InfoResultPayload"] = new Dictionary<string, object>()
                {
                    ["AccountInfo"] = new Dictionary<string, object>()
                    {
                        ["PlayFabId"] = userId.ToString(),
                        ["Created"] = account.CreatedDate.UtcDateTime,
                        ["TitleInfo"] = new Dictionary<string, object>()
                        {
                            ["Origination"] = "XboxLive",
                            ["Created"] = account.CreatedDate.UtcDateTime,
                            ["LastLogin"] = account.CreatedDate.UtcDateTime,
                            ["FirstLogin"] = account.CreatedDate.UtcDateTime,
                            ["isBanned"] = false,
                            ["TitlePlayerAccount"] = new Dictionary<string, string>()
                            {
                                ["Id"] = userId.ToString(),
                                ["Type"] = "title_player_account",
                                ["TypeString"] = "title_player_account",
                            },
                        },
                        ["PrivateInfo"] = new object(),
                        ["XboxInfo"] = new Dictionary<string, string>()
                        {
                            ["XboxUserId"] = userId.ToString(),
                            ["XboxUserSandbox"] = "RETAIL",
                        },
                    },
                    ["UserInventory"] = Array.Empty<object>(),
                    ["UserDataVersion"] = 0,
                    ["UserReadOnlyDataVersion"] = 0,
                    ["CharacterInventories"] = Array.Empty<object>(),
                    ["PlayerProfile"] = new Dictionary<string, string>()
                    {
                        ["PublisherId"] = "B63A0803D3653643",
                        ["TitleId"] = request.TitleId,
                        ["PlayerId"] = userId.ToString(),
                    },
                },
                ["EntityToken"] = new Dictionary<string, object>()
                {
                    ["EntityToken"] = entityTokenString,
                    ["TokenExpiration"] = entityTokenValidity.ExpiresDT,
                    ["Entity"] = new Dictionary<string, string>()
                    {
                        ["Id"] = entityToken.Id.ToString(),
                        ["Type"] = entityToken.Type,
                        ["TypeString"] = entityToken.Type,
                    },
                },
                ["TreatmentAssignment"] = new Dictionary<string, object>()
                {
                    ["Variants"] = Array.Empty<object>(),
                    ["Variables"] = Array.Empty<object>(),
                },
            }
        ));
    }

    [HttpPost("LinkXboxAccount")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Endpoints cannot be static")]
    public ContentHttpResult LinkXboxAccount()
        => JsonCamelCase(new PlayfabErrorResponse(
            401,
            "Unauthorized",
            "NotAuthenticated",
            1074,
            "This API method does not allow anonymous callers.",
            null
        ));

    [GeneratedRegex("^[0-9A-F]{5}$")]
    private static partial Regex GetTitleIdRegex();
}
