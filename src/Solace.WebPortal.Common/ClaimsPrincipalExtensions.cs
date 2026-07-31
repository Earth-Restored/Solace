using System.Globalization;
using System.Security.Claims;

namespace Solace.WebPortal.Common;

public static class ClaimsPrincipalExtensions
{
    extension(ClaimsPrincipal? principal)
    {
        public bool HasPermission(string permission)
            => principal?.HasClaim("Permission", permission) ?? false;

        public long GetIdLong()
        {
            var idString = principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!long.TryParse(idString, CultureInfo.InvariantCulture, out var id))
            {
                throw new UnauthorizedAccessException();
            }

            return id;
        }
    }
}
