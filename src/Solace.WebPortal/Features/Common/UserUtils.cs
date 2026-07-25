using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Solace.WebPortal.Common.Features.Roles;
using Solace.WebPortal.Data;

namespace Solace.WebPortal.Features.Common;

public static class UserUtils
{
    public static async Task<int> GetMinimumRolePosition(RoleManager<ApplicationRole> roleManager, IHttpContextAccessor httpContextAccessor)
    {
        var httpUser = httpContextAccessor.HttpContext?.User;
        var currentUserRoles = httpUser?.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList() ?? [];

        if (currentUserRoles.Count == 0)
        {
            return 9999;
        }

        var minPosition = await roleManager.Roles
            .AsNoTracking()
            .Where(r => currentUserRoles.Contains(r.Name!) && r.Name != RoleConstants.Default)
            .Select(r => (int?)r.Position)
            .MinAsync();

        return minPosition ?? 9999;
    }
}