using System.Diagnostics;
using System.Text.RegularExpressions;
using Immediate.Apis.Shared;
using Immediate.Handlers.Shared;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Solace.Common.Asp.Auth;
using Solace.DB;

namespace Solace.AuthServer.Features.XboxLive.Profile;

[Handler]
[MapGet("profile.xboxlive.com/users/{GtParam}/profile/settings")]
public sealed partial class GetProfileSettings(
    IHttpContextAccessor httpContextAccessor,
    CryptoSecrets cryptoSecrets,
    EarthDbContext earthDb,
    ILogger<GetProfileSettings> logger
)
{
    public sealed record Query
    {
        [FromRoute]
        public required string GtParam { get; init; }
    }

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

        string? gt;
        if (query.GtParam is "me")
        {
            gt = token.Username;
        }
        else
        {
            var gtMatch = GetGtRegex().Match(query.GtParam);

            gt = gtMatch.Success ? gtMatch.Groups["gt"].Value : null;
        }

        if (gt != token.Username)
        {
            return TypedResults.Unauthorized();
        }

        if (!httpContext.Request.Query.TryGetValue("settings", out var settings))
        {
            return TypedResults.BadRequest();
        }

        var account = await earthDb.Accounts
            .AsNoTracking()
            .FirstOrDefaultAsync(account => account.Id == token.UserId, cancellationToken);

        if (account is null)
        {
            return TypedResults.NotFound();
        }

        return TypedResults.Ok(new ProfileUtils.ProfileSettingsResponse([
            new ProfileUtils.ProfileUser(
                token.UserId,
                token.UserId,
                ProfileUtils.GetProfileFields(account, settings[0]?.Split(',') ?? [], httpContext.Request),
                false
            ),
        ]));
    }

    [GeneratedRegex(@"^gt\((?<gt>.*)\)$", RegexOptions.None, matchTimeoutMilliseconds: 200)]
    private static partial Regex GetGtRegex();
}