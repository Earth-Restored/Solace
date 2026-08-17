using System.Text.RegularExpressions;
using Immediate.Apis.Shared;
using Immediate.Handlers.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Solace.WebPortal.Common;

namespace Solace.WebPortal.Features.Catalog;

[Handler]
[MapGet("static/genoa-textures/ui/items/{name}.png")]
[Authorize]
public static partial class GetItemTexture
{
    public sealed record Query([property: FromRoute] string Name);

    private static async ValueTask<Results<PhysicalFileHttpResult, NotFound, UnauthorizedHttpResult, ForbidHttpResult>> HandleAsync(
        Query query,
        IConfiguration configuration,
        IHttpContextAccessor httpContextAccessor,
        CancellationToken cancellationToken
    )
    {
        var httpUser = httpContextAccessor.HttpContext?.User;
        if (httpUser is null)
        {
            return TypedResults.Unauthorized();
        }

        if (!httpUser.HasPermission(Permissions.CreateProfile) && !httpUser.HasPermission(Permissions.ViewPlayers) && !httpUser.HasPermission(Permissions.ViewStore))
        {
            return TypedResults.Forbid();
        }

        var staticDataPath = configuration["StaticDataPath"]!;

        var cachePath = await GenoaResourcepackCache.GetCachePath(staticDataPath);

        if (cachePath is null)
        {
            return TypedResults.NotFound();
        }

        if (!GetFileNameRegex().IsMatch(query.Name))
        {
            return TypedResults.NotFound();
        }

        var path = Path.Combine(cachePath, "textures", "ui", "items", query.Name + ".png");

        if (!File.Exists(path))
        {
            return TypedResults.NotFound();
        }

        return TypedResults.PhysicalFile(path);
    }

    [GeneratedRegex("^[a-zA-Z0-9_.\\- ]+$", RegexOptions.None, matchTimeoutMilliseconds: 200)]
    private static partial Regex GetFileNameRegex();
}
