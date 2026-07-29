namespace Solace.WebPortal.Common.Features.Users;

public sealed record UpdateUserRequest(
    string UserName,
    string Email,
    string? NewPassword,
    List<string> AssignedRoles
);