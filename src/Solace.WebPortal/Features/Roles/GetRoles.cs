using System.Security.Claims;
using Immediate.Apis.Shared;
using Immediate.Handlers.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Solace.WebPortal.Common;
using Solace.WebPortal.Common.Features.Common;
using Solace.WebPortal.Common.Features.Roles;
using Solace.WebPortal.Data;

namespace Solace.WebPortal.Features.Roles;

[Handler]
[MapGet("")]
[MapGroup<RolesGroup>]
[Authorize(Policy = Permissions.EditRoles)]
public static partial class GetRoles
{
    public sealed record Query;

    private static async ValueTask<GetRolesResponse> HandleAsync(
        Query _,
        RoleManager<ApplicationRole> roleManager,
        IHttpContextAccessor httpContextAccessor,
        CancellationToken cancellationToken)
    {
        var user = httpContextAccessor.HttpContext?.User;
        var userRoleNames = user?.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList() ?? [];

        var allRoles = await roleManager.Roles.ToListAsync(cancellationToken);

        var userMinPosition = allRoles
            .Where(r => userRoleNames.Contains(r.Name!))
            .Select(r => r.Position)
            .DefaultIfEmpty(9999)
            .Min();

        var roleDtos = new List<RoleDto>();
        foreach (var role in allRoles)
        {
            var claims = await roleManager.GetClaimsAsync(role);
            var permissions = claims
                .Where(c => c.Type == "Permission")
                .Select(c => c.Value)
                .Order(StringComparer.Ordinal)
                .ToList();

            roleDtos.Add(new RoleDto(
                role.Id,
                role.Name!,
                role.Position,
                role.Color,
                role.IsBuiltIn,
                permissions));
        }

        return new GetRolesResponse(roleDtos, userMinPosition);
    }
}
