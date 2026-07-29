namespace Solace.WebPortal.Common.Features.Users;

public sealed record UserDto(
    long Id,
    string UserName,
    string Email,
    IList<string> Roles
);
