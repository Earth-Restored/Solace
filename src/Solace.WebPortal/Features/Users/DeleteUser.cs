using System.Globalization;
using System.Security.Claims;
using Immediate.Apis.Shared;
using Immediate.Handlers.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Solace.Db.Earth;
using Solace.ObjectStore.Client;
using Solace.WebPortal.Common;
using Solace.WebPortal.Common.Features.Roles;
using Solace.WebPortal.Data;
using Solace.WebPortal.Features.Players;

namespace Solace.WebPortal.Features.Users;

[Handler]
[MapDelete("{userId}")]
[MapGroup<UsersGroup>]
[Authorize(Policy = Permissions.DeleteUsers)]
public static partial class DeleteUser
{
    public sealed record Command(
        [property: FromRoute] long UserId,
        [property: FromQuery] bool DeleteProfiles
    );

    private static async ValueTask<Results<UnauthorizedHttpResult, NotFound, BadRequest<string>, ProblemHttpResult, Ok>> HandleAsync(
        Command command,
        ApplicationDbContext webPortalDb,
        EarthDbContext earthDb,
        ObjectStoreClient objectStore,
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

        var targetUser = await userManager.FindByIdAsync(command.UserId.ToString(CultureInfo.InvariantCulture));
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

        await using var webPortalTransaction = await webPortalDb.Database.BeginTransactionAsync(cancellationToken);
        await using var earthTransaction = await earthDb.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            var result = await userManager.DeleteAsync(targetUser);

            if (!result.Succeeded)
            {
                return TypedResults.BadRequest(string.Join(", ", result.Errors.Select(e => e.Description)));
            }

            if (command.DeleteProfiles)
            {
                var profiles = await earthDb.Profiles
                    .AsNoTracking()
                    .Where(profile => profile.WebPortalAccountId == command.UserId)
                    .Select(profile => profile.Id)
                    .ToListAsync(cancellationToken);

                foreach (var profile in profiles)
                {
                    await webPortalDb.BuildplatePreviews
                        .Where(preview => preview.PlayerId == profile)
                        .ExecuteDeleteAsync(cancellationToken);

                    await ProfileDeleteUtil.DeleteProfile(profile, earthDb, objectStore, cancellationToken);
                }
            }
            else
            {
                await earthDb.Profiles
                    .Where(profile => profile.WebPortalAccountId == command.UserId)
                    .ExecuteUpdateAsync(setters => setters.SetProperty(profile => profile.WebPortalAccountId, (long?)null), cancellationToken);
            }

            await earthTransaction.CommitAsync(cancellationToken);
            await webPortalTransaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await earthTransaction.RollbackAsync(cancellationToken);
            await webPortalTransaction.RollbackAsync(cancellationToken);
            throw;
        }

        return TypedResults.Ok();
    }
}
