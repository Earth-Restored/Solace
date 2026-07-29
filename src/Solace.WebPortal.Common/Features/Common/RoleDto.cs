namespace Solace.WebPortal.Common.Features.Common;

public sealed record RoleDto(
    long Id,
    string Name,
    int Position,
    string Color,
    bool IsBuiltIn,
    List<string> Permissions
);
