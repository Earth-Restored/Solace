using Microsoft.AspNetCore.Identity;
using Solace.WebPortal.Utils;

namespace Solace.WebPortal.Data;

public sealed class ApplicationRole : IdentityRole<long>
{
    public const string Owner = "owner";
    public const string Default = "everyone";

    /// <summary>
    /// Initializes a new instance of <see cref="ApplicationRole"/>.
    /// </summary>
    public ApplicationRole()
    {
        Id = LongIdGenerator.NextId();
    }

    /// <summary>
    /// Initializes a new instance of <see cref="ApplicationRole"/>.
    /// </summary>
    /// <param name="roleName">The role name.</param>
    public ApplicationRole(string roleName)
        : this()
    {
        Name = roleName;
    }

    public string Color { get; set; } = "#99AAB5";

    public int Position { get; set; }

    public bool IsBuiltIn { get; set; }
}
