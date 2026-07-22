using System.Globalization;
using System.Security.Claims;
using Immediate.Apis.Shared;
using Immediate.Handlers.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Solace.WebPortal.Common;
using Solace.WebPortal.Common.Features.Roles;
using Solace.WebPortal.Data;
using Solace.WebPortal.Features.Common;

namespace Solace.WebPortal.Features.Roles;

[Handler]
[MapPut("")]
[MapGroup<RolesGroup>]
[Authorize(Policy = Permissions.EditRoles)]
public static partial class UpdateRole
{
    private static async ValueTask<IResult> HandleAsync(
        UpdateRoleCommand command,
        UserManager<ApplicationUser> userManager,
        RoleManager<ApplicationRole> roleManager,
        IHttpContextAccessor httpContextAccessor,
        CancellationToken cancellationToken)
    {
        var role = await roleManager.FindByIdAsync(command.Id.ToString(CultureInfo.InvariantCulture));
        if (role is null)
        {
            return Results.NotFound($"Role with ID {command.Id} not found.");
        }

        var currentUserMinPosition = await UserUtils.GetMinimumRolePosition(userManager, roleManager, httpContextAccessor);

        if (role.Position <= currentUserMinPosition)
        {
            return Results.NotFound("Cannot update role.");
        }

        if (role.Name != command.Name)
        {
            var existingRole = await roleManager.FindByNameAsync(command.Name);
            if (existingRole is not null && existingRole.Id != role.Id)
            {
                return Results.BadRequest($"A role with the name '{command.Name}' already exists.");
            }
        }

        if (!role.IsBuiltIn)
        {
            role.Name = command.Name;
            role.Color = command.Color;

            var updateResult = await roleManager.UpdateAsync(role);
            if (!updateResult.Succeeded)
            {
                return Results.BadRequest(updateResult.Errors);
            }
        }

        var currentClaims = await roleManager.GetClaimsAsync(role);
        foreach (var claim in currentClaims.Where(c => c.Type == "Permission"))
        {
            await roleManager.RemoveClaimAsync(role, claim);
        }

        foreach (var perm in command.Permissions)
        {
            await roleManager.AddClaimAsync(role, new Claim("Permission", perm));
        }

        return Results.Ok();
    }
}
