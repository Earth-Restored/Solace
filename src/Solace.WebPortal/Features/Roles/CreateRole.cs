using System.Security.Claims;
using Immediate.Apis.Shared;
using Immediate.Handlers.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Solace.WebPortal.Common;
using Solace.WebPortal.Common.Features.Roles;
using Solace.WebPortal.Data;

namespace Solace.WebPortal.Features.Roles;

[Handler]
[MapPost("/api/roles")]
[Authorize(Policy = Permissions.EditRoles)]
public static partial class CreateRole
{
    private static async ValueTask<Results<BadRequest<string>, Ok>> HandleAsync(
        CreateRoleCommand command,
        RoleManager<ApplicationRole> roleManager,
        CancellationToken cancellationToken)
    {
        if (await roleManager.RoleExistsAsync(command.Name))
        {
            return TypedResults.BadRequest($"Role '{command.Name}' already exists.");
        }

        var roleCount = await roleManager.Roles.CountAsync(cancellationToken);

        var role = new ApplicationRole
        {
            Name = command.Name.Trim(),
            Color = command.Color,
            Position = roleCount - 1,
        };

        var result = await roleManager.CreateAsync(role);
        if (!result.Succeeded)
        {
            return TypedResults.BadRequest("An error occured while creating the role.");
        }

        foreach (var perm in command.Permissions)
        {
            await roleManager.AddClaimAsync(role, new Claim("Permission", perm));
        }

        return TypedResults.Ok();
    }
}