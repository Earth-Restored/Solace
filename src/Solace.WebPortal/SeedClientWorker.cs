using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using OpenIddict.Abstractions;
using Solace.WebPortal.Data;

namespace Solace.WebPortal;

public sealed class SeedClientWorker(IServiceProvider serviceProvider, IConfiguration configuration) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await context.Database.MigrateAsync(cancellationToken);

        var manager = scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();

        var application = await manager.FindByClientIdAsync("client_app", cancellationToken);

        var authServerEndpoint = configuration["PublicEndpoints:AuthServer"];
        if (string.IsNullOrEmpty(authServerEndpoint))
        {
            authServerEndpoint = configuration["services:auth-server:http:0"]!;
        }

        var authServerOidcConfig = configuration.GetSection("Oidc:AuthServer").Get<Solace.Common.Asp.Oidc.OidcClientConfiguration>();
        Debug.Assert(authServerOidcConfig is not null);

        var descriptor = new OpenIddictApplicationDescriptor
        {
            ClientId = authServerOidcConfig.ClientId,
            ClientSecret = authServerOidcConfig.ClientSecret,
            ClientType = OpenIddictConstants.ClientTypes.Confidential,
            ConsentType = OpenIddictConstants.ConsentTypes.Explicit,
            DisplayName = authServerOidcConfig.DisplayName,
            RedirectUris = { new Uri(new Uri(authServerEndpoint), "signin-oidc") },
            PostLogoutRedirectUris = { new Uri(new Uri(authServerEndpoint), "signout-callback-oidc") },
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
