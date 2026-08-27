using Microsoft.AspNetCore.Identity;

namespace Solace.Db.Migrator.Old.Web;

public class ApplicationRole : IdentityRole
{
    public const string Owner = "owner";
    public const string Default = "everyone";

    public string Color { get; set; } = "#99AAB5";
    public int Position { get; set; }
    public bool IsBuiltIn { get; set; }
}