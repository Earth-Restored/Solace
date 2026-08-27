using Microsoft.AspNetCore.Identity;

namespace Solace.Db.Migrator.Old.Web;

public sealed class ApplicationUser : IdentityUser
{
    public List<Guid> LinkedInGameAccounts { get; set; } = [];
}
