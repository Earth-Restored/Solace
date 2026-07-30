using Immediate.Apis.Shared;
using Immediate.Handlers.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Solace.Db.Earth;
using Solace.WebPortal.Common;
using Solace.WebPortal.Common.Features.Players.Inventory;

namespace Solace.WebPortal.Features.Players.Inventory;

[Handler]
[MapGet("")]
[MapGroup<InventoryGroup>]
[Authorize]
public static partial class GetInventory
{
    public sealed record Query([property: FromRoute] Guid Id);

    private static async ValueTask<Results<Ok<GetInventoryResponse>, UnauthorizedHttpResult, ForbidHttpResult>> HandleAsync(
        Query query,
        EarthDbContext earthDb,
        IHttpContextAccessor httpContextAccessor,
        CancellationToken cancellationToken
    )
    {
        var httpUser = httpContextAccessor.HttpContext?.User;
        if (httpUser is null)
        {
            return TypedResults.Unauthorized();
        }

        if (!httpUser.HasPermission(Permissions.CreateProfile) && !httpUser.HasPermission(Permissions.ViewPlayers))
        {
            return TypedResults.Forbid();
        }

        var nonStackableItems = await earthDb.NonStackableItems
            .AsNoTracking()
            .Where(item => item.ProfileId == query.Id)
            .Select(item => new NonStackableItemDto(item.ItemId, item.Wear, item.InstanceId))
            .ToListAsync(cancellationToken);

        var stackableItems = await earthDb.StackableItems
            .AsNoTracking()
            .Where(item => item.ProfileId == query.Id)
            .Select(item => new StackableItemDto(item.ItemId, item.Count))
            .ToListAsync(cancellationToken);

        return TypedResults.Ok(new GetInventoryResponse(stackableItems, nonStackableItems));
    }
}
