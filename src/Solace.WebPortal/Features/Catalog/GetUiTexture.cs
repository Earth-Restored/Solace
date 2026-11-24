using System.Text.RegularExpressions;
using Immediate.Apis.Shared;
using Immediate.Handlers.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Solace.WebPortal.Common;

namespace Solace.WebPortal.Features.Catalog;

[Handler]
[MapGet("static/genoa-textures/ui/{*path}")]
[Authorize]
public static partial class GetUiTexture
{
    public sealed record Query([property: FromRoute] string Path);

    private static async ValueTask<Results<FileContentHttpResult, VirtualFileHttpResult, NotFound, UnauthorizedHttpResult, ForbidHttpResult>> HandleAsync(
        Query query,
        GenoaResourcepackCacheService resourcepackCache,
        IHttpContextAccessor httpContextAccessor,
        CancellationToken cancellationToken
    )
    {
        var httpUser = httpContextAccessor.HttpContext?.User;
        if (httpUser is null)
        {
            return TypedResults.Unauthorized();
        }

        if (!httpUser.HasPermission(Permissions.CreateProfile)
            && !httpUser.HasPermission(Permissions.ViewPlayers)
            && !httpUser.HasPermission(Permissions.ViewStore)
            && !httpUser.HasPermission(Permissions.ViewSeasonsAndChallenges))
        {
            return TypedResults.Forbid();
        }

        var normalizedPath = query.Path.TrimStart('/');
        if (!SafeTexturePathRegex().IsMatch(normalizedPath) || normalizedPath.Contains("..", StringComparison.Ordinal))
        {
            return TypedResults.NotFound();
        }

        var texture = await resourcepackCache.GetResourcePackFileAsync($"textures/ui/{normalizedPath}", cancellationToken);
        if (texture is not null)
        {
            return TypedResults.File(texture, contentType: "image/png");
        }

        return TypedResults.VirtualFile("/static/missing_texture.png", contentType: "image/png");
    }

    [GeneratedRegex(@"^[a-zA-Z0-9_.\- /]+\.png$", RegexOptions.IgnoreCase, matchTimeoutMilliseconds: 200)]
    private static partial Regex SafeTexturePathRegex();
}
