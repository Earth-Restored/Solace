namespace Solace.WebPortal.Common.Features.Roles;

public sealed record CreateRoleCommand(
    string Name,
    string Color,
    List<string> Permissions
);
