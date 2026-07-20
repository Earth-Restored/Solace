using Microsoft.AspNetCore.Authorization;

namespace Solace.AdminPanel;

internal sealed class PermissionRequirement(string permission) : IAuthorizationRequirement
{
    public string Permission { get; } = permission;
}
