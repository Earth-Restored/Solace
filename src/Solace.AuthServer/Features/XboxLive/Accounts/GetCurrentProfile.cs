using System.Diagnostics;
using System.Text.Json.Serialization;
using Immediate.Apis.Shared;
using Immediate.Handlers.Shared;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Solace.Common.Asp;
using Solace.DB;

namespace Solace.AuthServer.Features.XboxLive.Accounts;

[Handler]
[MapGet("accounts.xboxlive.com/users/current/profile")]
public sealed partial class GetCurrentProfile(
    IHttpContextAccessor httpContextAccessor,
    CryptoSecrets cryptoSecrets,
    EarthDbContext earthDb,
    ILogger<GetCurrentProfile> logger)
{
    public sealed record Query;

    [JsonNamingPolicy(JsonKnownNamingPolicy.CamelCase)]
    public sealed record Response(
        string? GamerTag,
        string? MidasConsole,
        DateTime TouAcceptanceDate,
        string? GamerTagChangeReason,
        DateTime DateOfBirth,
        DateTime DateCreated,
        string? Email,
        string? FirstName,
        string? HomeAddressInfo,
        string? HomeConsole,
        string? ImageUrl,
        bool IsAdult,
        string? LastName,
        string? LegalCountry,
        string? Locale,
        bool? MsftOptin,
        string? OwnerHash,
        string? OwnerXuid,
        bool? PartnerOptin,
        bool RequirePasskeyForPurchase,
        bool RequirePasskeyForSignIn,
        string? SubscriptionEntitlementInfo,
        string UserHash,
        string? UserKey,
        int UserXuid
    );

    private async ValueTask<Results<Ok<Response>, NotFound, UnauthorizedHttpResult, BadRequest>> HandleAsync(
        Query _,
        CancellationToken cancellationToken)
    {
        var httpContext = httpContextAccessor.HttpContext;
        Debug.Assert(httpContext is not null);

        var authUnion = AuthUtils.XboxLiveAuth(httpContext.Request, cryptoSecrets, logger);
        if (authUnion.IsB)
        {
            return authUnion.B.Result is UnauthorizedHttpResult unauthorized ? unauthorized : (BadRequest)authUnion.B.Result;
        }

        var token = authUnion.A;

        var account = await earthDb.Accounts
            .AsNoTracking()
            .Select(account => new { account.Id, account.Username, account.FirstName, account.LastName, account.CreatedDate, })
            .FirstOrDefaultAsync(account => account.Id == token.UserId, cancellationToken);

        if (account is null)
        {
            return TypedResults.NotFound();
        }

        return TypedResults.Ok(new Response(
            GamerTag: account.Username,
            MidasConsole: null,
            TouAcceptanceDate: new DateTime(1, 1, 1),
            GamerTagChangeReason: null,
            DateOfBirth: new DateTime(1, 1, 1),
            DateCreated: account.CreatedDate.UtcDateTime,
            Email: null,
            FirstName: account.FirstName,
            HomeAddressInfo: null,
            HomeConsole: null,
            ImageUrl: null,
            IsAdult: true,
            LastName: account.LastName,
            LegalCountry: null,
            Locale: null,
            MsftOptin: null,
            OwnerHash: null,
            OwnerXuid: null,
            PartnerOptin: null,
            RequirePasskeyForPurchase: false,
            RequirePasskeyForSignIn: false,
            SubscriptionEntitlementInfo: null,
            UserHash: token.UserId.ToString(),
            UserKey: null,
            UserXuid: 0
        ));
    }
}