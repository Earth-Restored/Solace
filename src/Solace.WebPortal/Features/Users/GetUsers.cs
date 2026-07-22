using System.Security.Claims;
using Immediate.Apis.Shared;
using Immediate.Handlers.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Solace.WebPortal.Common;
using Solace.WebPortal.Common.Features.Common;
using Solace.WebPortal.Common.Features.Roles;
using Solace.WebPortal.Common.Features.Users;
using Solace.WebPortal.Data;

namespace Solace.WebPortal.Features.Users;

[Handler]
[MapGet("")]
[MapGroup<UsersGroup>]
[Authorize(Policy = Permissions.ViewUsers)]
public static partial class GetUsers
{
    public sealed record Query(
        string? SearchTerm = null,
        int Page = 1,
        int PageSize = 10
    );

    private static async ValueTask<GetUsersResponse> HandleAsync(
        Query query,
        UserManager<ApplicationUser> userManager,
        RoleManager<ApplicationRole> roleManager,
        IHttpContextAccessor httpContextAccessor,
        CancellationToken cancellationToken)
    {
        var httpUser = httpContextAccessor.HttpContext?.User;
        var currentUserId = userManager.GetUserId(httpUser!) ?? string.Empty;

        var allRoles = await roleManager.Roles.AsNoTracking().ToListAsync(cancellationToken);
        var currentUserRoles = httpUser?.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList() ?? [];

        var currentUserMinPosition = allRoles
            .Where(r => currentUserRoles.Contains(r.Name!) && r.Name != RoleConstants.Default)
            .Select(r => r.Position)
            .DefaultIfEmpty(9999)
            .Min();

        var dbQuery = userManager.Users.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.SearchTerm))
        {
            var search = $"%{query.SearchTerm}%";

            dbQuery = dbQuery.Where(u =>
                (u.UserName != null && EF.Functions.ILike(u.UserName, search)) ||
                (u.Email != null && EF.Functions.ILike(u.Email, search)));
        }

        var totalUsers = await dbQuery.CountAsync(cancellationToken);
        var totalPages = (int)double.Ceiling(totalUsers / (double)query.PageSize);
        var page = int.Max(1, int.Min(query.Page, int.Max(1, totalPages)));

        var users = await dbQuery
            .OrderBy(u => u.UserName)
            .Skip((page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);

        var userDtos = new List<UserDto>(query.PageSize);
        foreach (var user in users)
        {
            var roles = await userManager.GetRolesAsync(user);
            userDtos.Add(new UserDto(user.Id, user.UserName ?? "", user.Email ?? "", roles));
        }

        var roleDtos = allRoles.Select(role => new RoleDto(role.Id, role.Name!, role.Position, role.Color, role.IsBuiltIn, [])).ToList();

        return new GetUsersResponse(
            userDtos,
            roleDtos,
            totalUsers,
            totalPages,
            page,
            currentUserMinPosition
        );
    }
}
