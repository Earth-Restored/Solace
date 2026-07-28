using System.Globalization;
using Immediate.Apis.Shared;
using Immediate.Handlers.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Solace.WebPortal.Common;
using Solace.WebPortal.Common.Features.Roles;
using Solace.WebPortal.Data;
using Solace.WebPortal.Features.Common;

namespace Solace.WebPortal.Features.Roles;

[Handler]
[MapPost("swap")]
[MapGroup<RolesGroup>]
[Authorize(Policy = Permissions.EditRoles)]
public static partial class SwapRoles
{
    private static async ValueTask<Results<NotFound, BadRequest<string>, Ok>> HandleAsync(
        SwapRolesCommand command,
        RoleManager<ApplicationRole> roleManager,
        IHttpContextAccessor httpContextAccessor,
        CancellationToken cancellationToken)
    {
        var roleA = await roleManager.FindByIdAsync(command.RoleIdA.ToString(CultureInfo.InvariantCulture));
        var roleB = await roleManager.FindByIdAsync(command.RoleIdB.ToString(CultureInfo.InvariantCulture));

        if (roleA is null || roleB is null)
        {
            return TypedResults.NotFound();
        }

        if (roleA.IsBuiltIn || roleB.IsBuiltIn)
        {
            return TypedResults.BadRequest("Cannot modify the position of built-in roles.");
        }

        if (int.Abs(roleA.Position - roleB.Position) != 1)
        {
            return TypedResults.BadRequest("Can only swap adjacent roles.");
        }

        var currentUserMinPosition = await UserUtils.GetMinimumRolePosition(roleManager, httpContextAccessor);

        if (roleA.Position <= currentUserMinPosition || roleB.Position <= currentUserMinPosition)
        {
            return TypedResults.BadRequest("Cannot swap the roles.");
        }

        var originalA = roleA.Position;
        var originalB = roleB.Position;

        // Perform the safe swap 
        roleA.Position = 10000;
        await roleManager.UpdateAsync(roleA);

        roleB.Position = originalA;
        await roleManager.UpdateAsync(roleB);

        roleA.Position = originalB;
        await roleManager.UpdateAsync(roleA);

        return TypedResults.Ok();
    }
}
