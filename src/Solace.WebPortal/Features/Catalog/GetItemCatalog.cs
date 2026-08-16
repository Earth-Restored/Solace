using System.Collections.Immutable;
using Immediate.Apis.Shared;
using Immediate.Handlers.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Solace.WebPortal.Common;
using Solace.WebPortal.Common.Features.Catalog;

namespace Solace.WebPortal.Features.Catalog;

[Handler]
[MapGet("items")]
[MapGroup<CatalogGroup>]
[Authorize]
public static partial class GetItemCatalog
{
    public sealed record Query;

    private static async ValueTask<Results<Ok<ImmutableArray<ItemDto>>, UnauthorizedHttpResult, ForbidHttpResult>> HandleAsync(
        Query _,
        CatalogResponseCacheService cacheService,
        IHttpContextAccessor httpContextAccessor,
        CancellationToken cancellationToken
    )
    {
        var httpUser = httpContextAccessor.HttpContext?.User;
        if (httpUser is null)
        {
            return TypedResults.Unauthorized();
        }

        if (!httpUser.HasPermission(Permissions.CreateProfile) && !httpUser.HasPermission(Permissions.ViewPlayers) && !httpUser.HasPermission(Permissions.ViewShop))
        {
            return TypedResults.Forbid();
        }

        return TypedResults.Ok(cacheService.GetItemsCatalog());
    }
}
