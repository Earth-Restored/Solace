using Microsoft.AspNetCore.Identity;
using Solace.WebPortal.Utils;

namespace Solace.WebPortal.Data;

// Add profile data for application users by adding properties to the ApplicationUser class
public sealed class ApplicationUser : IdentityUser<long>
{
    /// <summary>
    /// Initializes a new instance of <see cref="ApplicationUser"/>.
    /// </summary>
    public ApplicationUser()
    {
        Id = LongIdGenerator.NextId();
        SecurityStamp = Guid.NewGuid().ToString();
    }

    /// <summary>
    /// Initializes a new instance of <see cref="ApplicationUser"/>.
    /// </summary>
    /// <param name="userName">The user name.</param>
    public ApplicationUser(string userName)
        : this()
    {
        UserName = userName;
    }
}

