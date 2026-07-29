using Microsoft.AspNetCore.Authorization;

namespace Solace.WebPortal.Common;

public static class PermissionAuthorizationExtensions
{
    public static void AddPermissionPolicies(this AuthorizationOptions options)
    {
        foreach (var permission in Permissions.All)
        {
            options.AddPolicy(permission, policy =>
                policy.RequireClaim("Permission", permission));
        }
    }
}