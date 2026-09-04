using System.Text.Json.Serialization;
using Immediate.Apis.Shared;
using Immediate.Handlers.Shared;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Solace.AuthServer.Features.Common;
using Solace.Common.Asp.Auth;
using Solace.Common.Asp.Json;
using Solace.Db.Earth;

namespace Solace.AuthServer.Features.PlayfabApi.Login;

[Handler]
[MapPost("Client/LoginWithXbox")]
[MapGroup<PlayfabApiGroup>]
public sealed partial class LoginWithXbox(
    CryptoSecrets cryptoSecrets,
    EarthDbContext earthDb,
    IOptions<AuthSettings> authSettingsOptions,
    ILogger<LoginWithXbox> logger
)
{
    [ForcePascalCase]
    public sealed record Command(
        string TitleId,
        object? EncryptedRequest,
        object? PlayerSecret,
        bool CreateAccount,
        string XboxToken
    );

    [ForcePascalCase]
    public sealed record Response(
        string SessionTicket,
        Guid PlayFabId,
        bool NewlyCreated,
        SettingsForUser SettingsForUser,
        DateTime LastLoginTime,
        InfoResultPayload InfoResultPayload,
        EntityTokenR EntityToken,
        TreatmentAssignment TreatmentAssignment
    );

    [ForcePascalCase]
    public sealed record SettingsForUser(
        bool NeedsAttribution,
        bool GatherDeviceInfo,
        bool GatherFocusInfo
    );

    [ForcePascalCase]
    public sealed record InfoResultPayload(
        AccountInfo AccountInfo,
        object[] UserInventory,
        int UserDataVersion,
        int UserReadOnlyDataVersion,
        object[] CharacterInventories,
        PlayerProfile PlayerProfile
    );

    [ForcePascalCase]
    public sealed record AccountInfo(
        Guid PlayFabId,
        DateTime Created,
        TitleInfo TitleInfo,
        object PrivateInfo,
        XboxInfo XboxInfo
    );

    [ForcePascalCase]
    public sealed record TitleInfo(
        string Origination,
        DateTime Created,
        DateTime LastLogin,
        DateTime FirstLogin,
        [property: JsonPropertyName("isBanned")] bool IsBanned,
        ResponseEntity TitlePlayerAccount
    );

    [ForcePascalCase]
    public sealed record XboxInfo(
        Guid XboxUserId,
        string XboxUserSandbox
    );

    [ForcePascalCase]
    public sealed record PlayerProfile(
        string PublisherId,
        string TitleId,
        Guid PlayerId
    );

    [ForcePascalCase]
    public sealed record EntityTokenR(
        string EntityToken,
        DateTime TokenExpiration,
        ResponseEntity Entity
    );

    [ForcePascalCase]
    public sealed record TreatmentAssignment(
        object[] Variants,
        object[] Variables
    );

    private async ValueTask<Results<Ok<OkResponse<Response>>, ForbidHttpResult, NotFound, BadRequest<string>>> HandleAsync(
        Command command,
        CancellationToken cancellationToken
    )
    {
        var authSettings = authSettingsOptions.Value;

        if (!PlayfabApiUtils.GetTitleIdRegex().IsMatch(command.TitleId))
        {
            return TypedResults.BadRequest("");
        }

        var authorization = XboxAuthorizationUtils.Parse(command.XboxToken);

        if (authorization is not { } authValue)
        {
            return TypedResults.BadRequest("");
        }

        var xboxToken = JwtUtils.Verify<PlayfabXboxToken>(authValue.TokenString, cryptoSecrets.LivePlayfabTokenSecret, logger);

        if (xboxToken is null || xboxToken.Data.UserId != authValue.UserId)
        {
            // TODO: probably supposed to use a "fake 403" as with LoginWithCustomID
            return TypedResults.Forbid();
        }

        var userId = xboxToken.Data.UserId;

        var account = await earthDb.Profiles
            .AsNoTracking()
            .Select(account => new { account.Id, account.CreatedDate, })
            .FirstOrDefaultAsync(account => account.Id == userId, cancellationToken);

        if (account is null)
        {
            return TypedResults.NotFound();
        }

        var sessionTicketValidity = ValidityDatePair.Create(authSettings.SessionTicketValidityMinutes);
        var sessionTicket = new PlayfabSessionTicket(userId);
        var sessionTicketString = JwtUtils.Sign(sessionTicket, cryptoSecrets.PlayfabSessionTicketSecret, sessionTicketValidity);

        var entityTokenValidity = ValidityDatePair.Create(authSettings.EntityTokenValidityMinutes);
        var entityToken = new EntityToken(userId, "title_player_account");
        var entityTokenString = JwtUtils.Sign(entityToken, cryptoSecrets.PlayfabEntityTokenSecret, entityTokenValidity);

        return TypedResults.Ok(new OkResponse<Response>(
            200,
            "OK",
            new Response(
                $"{userId.ToString().ToUpperInvariant()}-{sessionTicketString}",
                userId,
                false,
                new SettingsForUser(
                    false,
                    true,
                    true
                ),
                account.CreatedDate.UtcDateTime,
                new InfoResultPayload(
                    new AccountInfo(
                        userId,
                        account.CreatedDate.UtcDateTime,
                        new TitleInfo(
                            "XboxLive",
                            account.CreatedDate.UtcDateTime,
                            account.CreatedDate.UtcDateTime,
                            account.CreatedDate.UtcDateTime,
                            false,
                            new ResponseEntity(
                                userId,
                                "title_player_account",
                                "title_player_account"
                            )
                        ),
                        new object(),
                        new XboxInfo(
                            userId,
                            "RETAIL"
                        )
                    ),
                    [],
                    0,
                    0,
                    [],
                    new PlayerProfile(
                        "B63A0803D3653643",
                        command.TitleId,
                        userId
                    )
                ),
                new EntityTokenR(
                    entityTokenString,
                    entityTokenValidity.ExpiresDT,
                    new ResponseEntity(
                        entityToken.Id,
                        entityToken.Type,
                        entityToken.Type
                    )
                ),
                new TreatmentAssignment(
                    [],
                    []
                )
            )
        ));
    }
}