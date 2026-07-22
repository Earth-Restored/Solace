namespace Solace.WebPortal.Common.Features.Roles;

public record UpdateRoleCommand(
    long Id,
    string Name,
    string Color,
    List<string> Permissions
);
