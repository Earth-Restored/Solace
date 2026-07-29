using Microsoft.EntityFrameworkCore;
using OpenIddict.Abstractions;
using Solace.WebPortal.Data;

namespace Solace.WebPortal;

public sealed class SeedClientWorker(IServiceProvider serviceProvider) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await context.Database.MigrateAsync(cancellationToken);

        var manager = scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();

        var application = await manager.FindByClientIdAsync("client_app", cancellationToken);

        var descriptor = new OpenIddictApplicationDescriptor
        {
            ClientId = "client_app",
            ClientSecret = "development_secret_change_in_prod",
            ClientType = OpenIddictConstants.ClientTypes.Confidential,
            ConsentType = OpenIddictConstants.ConsentTypes.Explicit,
            DisplayName = "Game Client App",
            RedirectUris = { new Uri("http://localhost:8088/signin-oidc") },
            PostLogoutRedirectUris = { new Uri("http://localhost:8088/signout-callback-oidc") },
            Permissions =
            {
                OpenIddictConstants.Permissions.Endpoints.Authorization,
                OpenIddictConstants.Permissions.Endpoints.EndSession,
                OpenIddictConstants.Permissions.Endpoints.Token,
                OpenIddictConstants.Permissions.GrantTypes.AuthorizationCode,
                OpenIddictConstants.Permissions.GrantTypes.RefreshToken,
                OpenIddictConstants.Permissions.ResponseTypes.Code,
                OpenIddictConstants.Permissions.Scopes.Email,
                OpenIddictConstants.Permissions.Scopes.Profile
            }
        };

        if (application is null)
        {
            await manager.CreateAsync(descriptor, cancellationToken);
        }
        else
        {
            await manager.UpdateAsync(application, descriptor, cancellationToken);
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
    }
}
