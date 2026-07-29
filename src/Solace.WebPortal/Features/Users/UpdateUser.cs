using System.Globalization;
using System.Security.Claims;
using Immediate.Apis.Shared;
using Immediate.Handlers.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Solace.WebPortal.Common;
using Solace.WebPortal.Common.Features.Roles;
using Solace.WebPortal.Common.Features.Users;
using Solace.WebPortal.Data;

namespace Solace.WebPortal.Features.Users;

[Handler]
[MapPut("{id}")]
[MapGroup<UsersGroup>]
[Authorize]
public static partial class UpdateUser
{
    public sealed record Command([property: FromRoute] long Id, [property: FromBody] UpdateUserRequest Request);

    private static async ValueTask<Results<UnauthorizedHttpResult, ForbidHttpResult, NotFound<string>, ProblemHttpResult, BadRequest<string>, Ok>> HandleAsync(
        Command command,
        UserManager<ApplicationUser> userManager,
        RoleManager<ApplicationRole> roleManager,
        IHttpContextAccessor httpContextAccessor,
        CancellationToken cancellationToken)
    {
        var httpUser = httpContextAccessor.HttpContext?.User;
        if (httpUser is null)
        {
            return TypedResults.Unauthorized();
        }

        var canEditAccount = httpUser.HasPermission(Permissions.EditAcountInfo);
        var canAssignRoles = httpUser.HasPermission(Permissions.AssignRoles);

        if (!canEditAccount && !canAssignRoles)
        {
            return TypedResults.Forbid();
        }

        if (command.Request is null)
        {
            return TypedResults.BadRequest("Request body cannot be null.");
        }

        var targetUser = await userManager.FindByIdAsync(command.Id.ToString(CultureInfo.InvariantCulture));
        if (targetUser is null)
        {
            return TypedResults.NotFound("User not found.");
        }

        var allRoles = await roleManager.Roles.AsNoTracking().ToListAsync(cancellationToken);
        var currentUserRoles = httpUser.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList();
        var currentUserId = long.Parse(userManager.GetUserId(httpUser)!, CultureInfo.InvariantCulture);

        var userMinPosition = allRoles
            .Where(role => currentUserRoles.Contains(role.Name!) && role.Name != RoleConstants.Default)
            .Select(role => role.Position)
            .DefaultIfEmpty(9999)
            .Min();

        var targetCurrentRoles = await userManager.GetRolesAsync(targetUser);
        var targetMinPos = allRoles
            .Where(role => targetCurrentRoles.Contains(role.Name!) && role.Name != RoleConstants.Default)
            .Select(role => role.Position)
            .DefaultIfEmpty(9999)
            .Min();

        if (targetMinPos <= userMinPosition && targetUser.Id != currentUserId)
        {
            return TypedResults.Problem("You cannot edit a user with equal or higher rank.", statusCode: 403);
        }

        if (canEditAccount)
        {
            if (targetUser.UserName != command.Request.UserName)
            {
                var result = await userManager.SetUserNameAsync(targetUser, command.Request.UserName);
                if (!result.Succeeded)
                {
                    return TypedResults.BadRequest(string.Join(", ", result.Errors.Select(e => e.Description)));
                }
            }

            if (targetUser.Email != command.Request.Email)
            {
                var result = await userManager.SetEmailAsync(targetUser, command.Request.Email);
                if (!result.Succeeded)
                {
                    return TypedResults.BadRequest(string.Join(", ", result.Errors.Select(e => e.Description)));
                }
            }

            if (!string.IsNullOrWhiteSpace(command.Request.NewPassword))
            {
                await userManager.RemovePasswordAsync(targetUser);
                var result = await userManager.AddPasswordAsync(targetUser, command.Request.NewPassword);
                if (!result.Succeeded)
                {
                    return TypedResults.BadRequest(string.Join(", ", result.Errors.Select(e => e.Description)));
                }
            }
        }

        if (canAssignRoles)
        {
            var requestedRoles = command.Request.AssignedRoles ?? [];

            var rolesToAdd = requestedRoles.Except(targetCurrentRoles, StringComparer.Ordinal).ToList();
            var rolesToRemove = targetCurrentRoles.Except(requestedRoles, StringComparer.Ordinal).ToList();

            rolesToAdd = [.. rolesToAdd.Where(roleName =>
                allRoles.Any(role => role.Name == roleName && role.Position > userMinPosition && !role.IsBuiltIn))];

            rolesToRemove = [.. rolesToRemove.Where(roleName =>
                allRoles.Any(role => role.Name == roleName && role.Position > userMinPosition && !role.IsBuiltIn))];

            if (rolesToAdd.Count is not 0)
            {
                await userManager.AddToRolesAsync(targetUser, rolesToAdd);
            }

            if (rolesToRemove.Count is not 0)
            {
                await userManager.RemoveFromRolesAsync(targetUser, rolesToRemove);
            }
        }

        return TypedResults.Ok();
    }
}
