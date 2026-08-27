using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Encodings.Web;

namespace Solace.ApiServer.Authentication;

public sealed class GenoaAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{public const string DataProtectionPurpose = "Solace.Genoa.AuthTokens";

    private readonly ITimeLimitedDataProtector _protector;

    public GenoaAuthenticationHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder, IDataProtectionProvider dataProtectionProvider)
        : base(options, logger, encoder)
    {
        _protector = dataProtectionProvider.CreateProtector(DataProtectionPurpose).ToTimeLimitedDataProtector();
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        await Task.Yield();

        // skip authentication if endpoint has [AllowAnonymous] attribute
        var endpoint = Context.GetEndpoint();
        if (endpoint?.Metadata?.GetMetadata<IAllowAnonymous>() is not null)
        {
            return AuthenticateResult.NoResult();
        }

        // Check if we should really authenticate
        if (endpoint?.Metadata?.GetMetadata<IAuthorizeData>() is null)
        {
            return AuthenticateResult.NoResult();
        }

        if (!Request.Headers.ContainsKey("Authorization"))
        {
            return AuthenticateResult.Fail("Missing Authorization Header");
        }

       string? encryptedToken;
        try
        {
            if (!Request.Headers.TryGetValue("Authorization", out StringValues authorization))
            {
                return AuthenticateResult.Fail("Invalid Authorization Header");
            }

            var authHeader = AuthenticationHeaderValue.Parse(authorization.ToString());
            if (authHeader.Scheme == "Genoa")
            {
                encryptedToken = authHeader.Parameter;
            }
            else
            {
                return AuthenticateResult.Fail("Invalid Authorization Header");
            }
        }
        catch
        {
            return AuthenticateResult.Fail("Invalid Authorization Header");
        }

        if (encryptedToken is null)
        {
            return AuthenticateResult.Fail("Invalid Authorization Header");
        }

        string decryptedUserId;
        try
        {
            decryptedUserId = _protector.Unprotect(encryptedToken);
        }
        catch (CryptographicException)
        {
            return AuthenticateResult.Fail("Invalid or expired session token.");
        }

        var claims = new[] { new Claim(ClaimTypes.NameIdentifier, decryptedUserId.ToLowerInvariant()), };

        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);

        return AuthenticateResult.Success(ticket);
    }
}
