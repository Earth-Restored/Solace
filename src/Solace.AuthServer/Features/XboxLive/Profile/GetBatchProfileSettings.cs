using System.Diagnostics;
using System.Text.Json.Serialization;
using Immediate.Apis.Shared;
using Immediate.Handlers.Shared;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Solace.Common.Asp.Auth;
using Solace.DB;

namespace Solace.AuthServer.Features.XboxLive.Profile;

[Handler]
[MapPost("profile.xboxlive.com/users/batch/profile/settings")]
public sealed partial class GetBatchProfileSettings(
    IHttpContextAccessor httpContextAccessor,
    CryptoSecrets cryptoSecrets,
    EarthDbContext earthDb,
    ILogger<GetBatchProfileSettings> logger
)
{
    [JsonNamingPolicy(JsonKnownNamingPolicy.CamelCase)]
    public sealed record Query(
        string[] Settings,
        Guid[] UserIds
    );

    private async ValueTask<Results<Ok<ProfileUtils.ProfileSettingsResponse>, NotFound, UnauthorizedHttpResult, BadRequest>> HandleAsync(
       Query query,
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

        foreach (var userId in query.UserIds)
        {
            if (userId != token.UserId)
            {
                return TypedResults.Unauthorized();
            }
        }

        var account = await earthDb.Accounts
            .AsNoTracking()
            .FirstOrDefaultAsync(account => account.Id == token.UserId, cancellationToken);

        if (account is null)
        {
            return TypedResults.NotFound();
        }

        return TypedResults.Ok(new ProfileUtils.ProfileSettingsResponse(
            query.UserIds.Select(userId
                => new ProfileUtils.ProfileUser(
                    userId,
                    userId,
                    ProfileUtils.GetProfileFields(account, query.Settings, httpContext.Request),
                    false
                )
            )
        ));
    }
}