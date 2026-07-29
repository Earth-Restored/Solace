using System.Security.Claims;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using OpenIddict.Validation.AspNetCore;
using Solace.WebPortal.Common;
using Solace.WebPortal.Data;

namespace Solace.WebPortal.Features.Oidc;

public static class OidcEndpoints
{
    public static void MapOidcEndpoints(this WebApplication app)
    {
        app.MapGet("/connect/authorize", async (HttpContext context, SignInManager<ApplicationUser> signInManager) =>
        {
            var request = context.GetOpenIddictServerRequest() ?? throw new InvalidOperationException("Invalid OIDC request.");

            var result = await context.AuthenticateAsync(IdentityConstants.ApplicationScheme);
            if (!result.Succeeded)
            {
                return Results.Challenge(new AuthenticationProperties
                {
                    RedirectUri = context.Request.PathBase + context.Request.Path + QueryString.Create(context.Request.Query.ToList())
                }, [IdentityConstants.ApplicationScheme]);
            }

            var user = await signInManager.UserManager.GetUserAsync(result.Principal)
                ?? throw new InvalidOperationException("User not found.");

            var principal = await signInManager.CreateUserPrincipalAsync((await signInManager.UserManager.GetUserAsync(result.Principal))!);
            var identity = (ClaimsIdentity)principal.Identity!;

            if (!identity.HasClaim(c => c.Type == OpenIddictConstants.Claims.Subject))
            {
                var userId = await signInManager.UserManager.GetUserIdAsync(user);
                identity.AddClaim(new Claim(OpenIddictConstants.Claims.Subject, userId));
            }

            principal.SetScopes(request.GetScopes());

            foreach (var claim in principal.Claims)
            {
                var destinations = new List<string> { OpenIddictConstants.Destinations.AccessToken };

                if (claim.Type is ClaimTypes.NameIdentifier or OpenIddictConstants.Claims.Subject or ClaimTypes.Email or OpenIddictConstants.Claims.Email)
                {
                    destinations.Add(OpenIddictConstants.Destinations.IdentityToken);
                }

                claim.SetDestinations(destinations);
            }

            return Results.SignIn(principal, properties: null, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        });

        app.MapPost("/connect/token", async (HttpContext context) =>
        {
            var request = context.GetOpenIddictServerRequest();
            if (request is not null && (request.IsAuthorizationCodeGrantType() || request.IsRefreshTokenGrantType()))
            {
                var result = await context.AuthenticateAsync(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
                if (!result.Succeeded)
                {
                    return Results.Forbid();
                }

                return Results.SignIn(result.Principal, properties: null, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
            }

            return Results.Forbid();
        });

        app.MapMethods("/connect/userinfo", ["GET", "POST"], async (HttpContext context, UserManager<ApplicationUser> userManager, IUserClaimsPrincipalFactory<ApplicationUser> principalFactory) =>
        {
            var result = await context.AuthenticateAsync(OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme);

            if (result.Principal is null)
            {
                return Results.Challenge(properties: null, [OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme]);
            }

            var userId = result.Principal.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? result.Principal.FindFirst(OpenIddictConstants.Claims.Subject)?.Value;

            if (string.IsNullOrEmpty(userId))
            {
                return Results.Challenge(properties: null, [OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme]);
            }

            var user = await userManager.FindByIdAsync(userId);
            if (user is null)
            {
                return Results.Challenge(properties: null, [OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme]);
            }

            var claims = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                [OpenIddictConstants.Claims.Subject] = userId
            };

            if (result.Principal.HasScope(OpenIddictConstants.Scopes.Email))
            {
                var email = await userManager.GetEmailAsync(user);
                if (!string.IsNullOrEmpty(email))
                {
                    claims[OpenIddictConstants.Claims.Email] = email;
                }
            }

            if (result.Principal.HasScope(OpenIddictConstants.Scopes.Profile))
            {
                var fullDbPrincipal = await principalFactory.CreateAsync(user);

                claims["can_create_profile"] = fullDbPrincipal.HasPermission(Permissions.CreateProfile) ? "true" : "false";
            }

            return Results.Ok(claims);
        });

        app.MapGet("/connect/logout", async (HttpContext context) =>
        {
            await context.SignOutAsync(IdentityConstants.ApplicationScheme);
            return Results.SignOut(properties: new AuthenticationProperties { RedirectUri = "/" }, [OpenIddictServerAspNetCoreDefaults.AuthenticationScheme]);
        });
    }
}
