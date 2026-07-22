using Solace.WebPortal.Common.Features.Common;

namespace Solace.WebPortal.Common.Features.Users;

public sealed record GetUsersResponse(
    List<UserDto> Users,
    List<RoleDto> Roles,
    int TotalUsers,
    int TotalPages,
    int CurrentPage,
    int CurrentUserMinPosition
);
