using System.Globalization;
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

namespace Solace.WebPortal.Features.Users;

[Handler]
[MapDelete("{id}")]
[MapGroup<UsersGroup>]
[Authorize(Policy = Permissions.DeleteUsers)]
public static partial class DeleteUser
{
    public sealed record Command(long Id);

    private static async ValueTask<Results<UnauthorizedHttpResult, NotFound, BadRequest<string>, ProblemHttpResult, Ok>> HandleAsync(
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

        var targetUser = await userManager.FindByIdAsync(command.Id.ToString(CultureInfo.InvariantCulture));
        if (targetUser is null)
        {
            return TypedResults.NotFound();
        }

        var currentUserId = long.Parse(userManager.GetUserId(httpUser)!, CultureInfo.InvariantCulture);
        if (targetUser.Id == currentUserId)
        {
            return TypedResults.BadRequest("Cannot delete your own account.");
        }

        var allRoles = await roleManager.Roles.AsNoTracking().ToListAsync(cancellationToken);
        var currentUserRoles = httpUser.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList();

        var userMinPos = allRoles
            .Where(role => currentUserRoles.Contains(role.Name!) && role.Name != RoleConstants.Default)
            .Select(r => r.Position)
            .DefaultIfEmpty(9999)
            .Min();

        var targetRoles = await userManager.GetRolesAsync(targetUser);
        var targetMinPos = allRoles
            .Where(role => targetRoles.Contains(role.Name!) && role.Name != RoleConstants.Default)
            .Select(r => r.Position)
            .DefaultIfEmpty(9999)
            .Min();

        if (targetMinPos <= userMinPos)
        {
            return TypedResults.Problem("You cannot delete a user with equal or higher rank.", statusCode: 403);
        }

        var result = await userManager.DeleteAsync(targetUser);
        return result.Succeeded ? TypedResults.Ok() : TypedResults.BadRequest(string.Join(", ", result.Errors.Select(e => e.Description)));
    }
}
