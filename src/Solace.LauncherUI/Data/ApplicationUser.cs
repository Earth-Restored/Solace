using Microsoft.AspNetCore.Identity;

namespace Solace.LauncherUI.Data;

public sealed class ApplicationUser : IdentityUser
{
    public List<Guid> LinkedInGameAccounts { get; set; } = [];
}
