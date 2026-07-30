namespace Solace.Common.Asp.Oidc;

public sealed record OidcClientConfiguration(
    string ClientId,
    string ClientSecret,
    string DisplayName
);
