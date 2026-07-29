using Solace.WebPortal.Common.Features.Common;

namespace Solace.WebPortal.Common.Features.Roles;

public sealed record GetRolesResponse(
    List<RoleDto> Roles,
    int UserMinPosition
);
