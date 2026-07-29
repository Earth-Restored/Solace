using System.Globalization;
using Immediate.Apis.Shared;
using Immediate.Handlers.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Solace.WebPortal.Common;
using Solace.WebPortal.Data;
using Solace.WebPortal.Features.Common;

namespace Solace.WebPortal.Features.Roles;

[Handler]
[MapDelete("/api/roles/{id}")]
[Authorize(Policy = Permissions.EditRoles)]
public static partial class DeleteRole
{
    public sealed record Command(long Id);

    private static async ValueTask<Results<NotFound, BadRequest<string>, Ok>> HandleAsync(
        Command command,
        RoleManager<ApplicationRole> roleManager,
        IHttpContextAccessor httpContextAccessor,
        CancellationToken token)
    {
        var role = await roleManager.FindByIdAsync(command.Id.ToString(CultureInfo.InvariantCulture));
        if (role is null)
        {
            return TypedResults.NotFound();
        }

        if (role.IsBuiltIn)
        {
            return TypedResults.BadRequest("Built-in roles cannot be deleted.");
        }

        var currentUserMinPosition = await UserUtils.GetMinimumRolePosition(roleManager, httpContextAccessor);

        if (role.Position <= currentUserMinPosition)
        {
            return TypedResults.BadRequest("Role cannot be deleted.");
        }

        var rolesToShift = await roleManager.Roles
            .Where(r => r.Position > role.Position)
            .OrderBy(r => r.Position)
            .ToListAsync(token);

        var result = await roleManager.DeleteAsync(role);

        if (!result.Succeeded)
        {
            return TypedResults.BadRequest("An error occured while deleting the role.");
        }

        foreach (var roleToShift in rolesToShift)
        {
            if (roleToShift.IsBuiltIn)
            {
                continue;
            }

            roleToShift.Position--;
            await roleManager.UpdateAsync(roleToShift);
        }

        return TypedResults.Ok();
    }
}
