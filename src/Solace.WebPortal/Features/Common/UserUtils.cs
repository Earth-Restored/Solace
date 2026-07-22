using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Solace.WebPortal.Common.Features.Roles;
using Solace.WebPortal.Data;

namespace Solace.WebPortal.Features.Common;

public static class UserUtils
{
    public static async Task<int> GetMinimumRolePosition(UserManager<ApplicationUser> userManager, RoleManager<ApplicationRole> roleManager, IHttpContextAccessor httpContextAccessor)
    {
        var httpUser = httpContextAccessor.HttpContext?.User;
        var currentUserId = userManager.GetUserId(httpUser!) ?? string.Empty;

        var currentUserRoles = httpUser?.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList() ?? [];
        var allRoles = roleManager.Roles.AsNoTracking();

        return await allRoles
            .Where(r => currentUserRoles.Contains(r.Name!) && r.Name != RoleConstants.Default)
            .Select(r => r.Position)
            .DefaultIfEmpty(9999)
            .MinAsync();
    }
}