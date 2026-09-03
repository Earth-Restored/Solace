using System.Diagnostics;
using System.Text.RegularExpressions;
using Aspire.Hosting;
using Aspire.Hosting.Testing;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Solace.Db.Earth;
using Solace.WebPortal.Common.Features.Roles;
using Solace.WebPortal.Data;
using TUnit.Core.Interfaces;

namespace Solace.IntegrationTests;

public sealed partial class LoginTests : IAsyncInitializer, IAsyncDisposable
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromMinutes(6);

    private const string AccountEmail = "test@solace.com";
    private const string AccountPassword = "aA1234$";

    private const string ProfileUsername = "profile";
    private Guid _profileId;

    private string _earthConnectionString = null!;

    private DistributedApplication _app = null!;
    private HttpClient _authServerClient = null!;
    private HttpClient _webPortalClient = null!;

    public async Task InitializeAsync()
    {
        _app = await AppHostExtensions.RunAsync(
            ["postgres", "event-bus", "auth-server", "object-store", "web-portal"],
            [
                // use default (localhost + port)
                "Shared:PublicEndpoints:WebPortal=",
                "Shared:PublicEndpoints:Locator=",
                "Shared:PublicEndpoints:AuthServer=",
                "Shared:PublicEndpoints:ApiServer=",
                "Shared:PublicEndpoints:Cdn=",
            ]);

        _authServerClient = _app.CreateHttpClient("auth-server", "http");
        _webPortalClient = _app.CreateHttpClient("web-portal", "http");

        await _app.ResourceNotifications.WaitForResourceHealthyAsync("auth-server")
            .WaitAsync(DefaultTimeout);

        await _app.ResourceNotifications.WaitForResourceHealthyAsync("web-portal")
            .WaitAsync(DefaultTimeout);

        var webPortalConnectionString = await _app.GetConnectionStringAsync("WebPortalDb");
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

        var earthConnectionString = await _app.GetConnectionStringAsync("EarthDb");
        Debug.Assert(earthConnectionString is not null);
        _earthConnectionString = earthConnectionString;

        await using var earthDb = EarthDbContext.CreateFromConnection(earthConnectionString);

        var profile = await earthDb.GetOrCreateAccount(Guid.CreateVersion7(), null);
        profile.Username = ProfileUsername;
        await earthDb.SaveChangesAsync();

        _profileId = profile.Id;
    }

    private static string ExtractInputValue(string html, string inputName)
    {
        var match = Regex.Match(
            html,
            $@"<input[^>]*name=""{Regex.Escape(inputName)}""[^>]*value=""([^""]*)""",
            RegexOptions.IgnoreCase,
            matchTimeout: TimeSpan.FromSeconds(1));

        if (!match.Success)
        {
            match = Regex.Match(
                html,
                $@"<input[^>]*value=""([^""]*)""[^>]*name=""{Regex.Escape(inputName)}""",
                RegexOptions.IgnoreCase, matchTimeout:
                TimeSpan.FromSeconds(1));
        }

        return match.Success ? match.Groups[1].Value : string.Empty;
    }

    private static string ExtractFormAction(string html)
    {
        var match = Regex.Match(
            html,
            "<form[^>]*action=\"(?<action>[^\"]*)\"",
            RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture,
            matchTimeout: TimeSpan.FromSeconds(1));

        return match.Success ? match.Groups["action"].Value : string.Empty;
    }

    private static IEnumerable<KeyValuePair<string, string>> ExtractFormInputs(string html)
    {
        foreach (Match match in Regex.Matches(
            html,
            "<input[^>]*name=\"(?<name>[^\"]+)\"[^>]*value=\"(?<value>[^\"]*)\"",
            RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture,
            matchTimeout: TimeSpan.FromSeconds(1)))
        {
            yield return new KeyValuePair<string, string>(
                match.Groups["name"].Value,
                match.Groups["value"].Value
            );
        }
    }

    public async ValueTask DisposeAsync()
    {
        _webPortalClient.Dispose();
        _authServerClient.Dispose();

        await _app.DisposeAsync();
    }
}
