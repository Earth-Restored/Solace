using System.Security.Claims;

namespace Solace.WebPortal.Common;

public static class ClaimsPrincipalExtensions
{
    extension(ClaimsPrincipal? principal)
    {
        public bool HasPermission(string permission)
            => principal?.HasClaim("Permission", permission) ?? false;
    }
}
