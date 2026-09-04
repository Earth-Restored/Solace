using System.Diagnostics;
using Aspire.Hosting;
using Aspire.Hosting.Testing;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Solace.Db.Earth;
using Solace.WebPortal.Common.Features.Roles;
using Solace.WebPortal.Data;
using TUnit.Core.Interfaces;

namespace Solace.IntegrationTests;

public sealed class LoginTestsFixture : IAsyncInitializer, IAsyncDisposable
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromMinutes(15);

    public const string AccountEmail = "login.tests@solace.com";
    public const string AccountPassword = "aA1234$";

    public const string ProfileUsername = "login_tests";
    public Guid ProfileId { get; private set; }

    public string EarthConnectionString { get; private set; } = null!;

    public DistributedApplication App { get; private set; } = null!;
    public HttpClient AuthServerClient { get; private set; } = null!;
    public HttpClient WebPortalClient { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        App = await AppHostExtensions.RunAsync(
            ["postgres", "event-bus", "auth-server", "object-store", "web-portal"],
            [
                // use default (localhost + port)
                "Shared:PublicEndpoints:WebPortal=",
                "Shared:PublicEndpoints:Locator=",
                "Shared:PublicEndpoints:AuthServer=",
                "Shared:PublicEndpoints:ApiServer=",
                "Shared:PublicEndpoints:Cdn=",
            ]);

        AuthServerClient = App.CreateHttpClient("auth-server", "http");
        WebPortalClient = App.CreateHttpClient("web-portal", "http");

        await App.ResourceNotifications.WaitForResourceHealthyAsync("auth-server")
            .WaitAsync(DefaultTimeout);

        await App.ResourceNotifications.WaitForResourceHealthyAsync("web-portal")
            .WaitAsync(DefaultTimeout);

        var webPortalConnectionString = await App.GetConnectionStringAsync("WebPortalDb");
        Debug.Assert(webPortalConnectionString is not null);

        await using var webPortalDb = ApplicationDbContext.CreateFromConnection(webPortalConnectionString);

        var user = new ApplicationUser()
        {
            UserName = AccountEmail,
            NormalizedUserName = AccountEmail.ToUpperOrdinal(),
            Email = AccountEmail,
            NormalizedEmail = AccountEmail.ToUpperOrdinal(),
            EmailConfirmed = true,
        };
        var passwordHasher = new PasswordHasher<ApplicationUser>();
        user.PasswordHash = passwordHasher.HashPassword(user, AccountPassword);

        webPortalDb.Users.Add(user);

        await webPortalDb.SaveChangesAsync();

        var ownerRole = await webPortalDb.Roles
            .AsNoTracking()
            .FirstAsync(role => role.Name == RoleConstants.Owner);

        webPortalDb.UserRoles.Add(new IdentityUserRole<long>()
        {
            UserId = user.Id,
            RoleId = ownerRole.Id,
        });

        await webPortalDb.SaveChangesAsync();

        var earthConnectionString = await App.GetConnectionStringAsync("EarthDb");
        Debug.Assert(earthConnectionString is not null);
        EarthConnectionString = earthConnectionString;

        await using var earthDb = EarthDbContext.CreateFromConnection(earthConnectionString);

        var profile = await earthDb.GetOrCreateAccount(Guid.CreateVersion7(), null);
        profile.Username = ProfileUsername;
        await earthDb.SaveChangesAsync();

        ProfileId = profile.Id;
    }

    public async ValueTask DisposeAsync()
    {
        WebPortalClient.Dispose();
        AuthServerClient.Dispose();

        await App.DisposeAsync();
    }
}
