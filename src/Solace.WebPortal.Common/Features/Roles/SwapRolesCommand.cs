namespace Solace.WebPortal.Common.Features.Roles;

public sealed record SwapRolesCommand(
    long RoleIdA,
    long RoleIdB
);
