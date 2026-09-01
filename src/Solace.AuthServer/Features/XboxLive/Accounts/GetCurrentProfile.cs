using System.Diagnostics;
using System.Text.Json.Serialization;
using Immediate.Apis.Shared;
using Immediate.Handlers.Shared;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Solace.Common.Asp.Auth;
using Solace.Db.Earth;

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
        if (authUnion is not XapiToken token)
        {
            var results = (Results<UnauthorizedHttpResult, BadRequest>)authUnion.Value!;
            return results.Result is UnauthorizedHttpResult unauthorized ? unauthorized : (BadRequest)results.Result;
        }

        var profile = await earthDb.Profiles
            .AsNoTracking()
            .Select(profile => new { profile.Id, profile.Username, profile.CreatedDate, })
            .FirstOrDefaultAsync(profile => profile.Id == token.UserId, cancellationToken);

        if (profile is null)
        {
            return TypedResults.NotFound();
        }

        return TypedResults.Ok(new Response(
            GamerTag: profile.Username,
            MidasConsole: null,
            TouAcceptanceDate: new DateTime(1, 1, 1),
            GamerTagChangeReason: null,
            DateOfBirth: new DateTime(1, 1, 1),
            DateCreated: profile.CreatedDate.UtcDateTime,
            Email: null,
            FirstName: null,
            HomeAddressInfo: null,
            HomeConsole: null,
            ImageUrl: null,
            IsAdult: true,
            LastName: null,
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