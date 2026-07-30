namespace Solace.Common.Asp.Oidc;

public sealed record OidcServerConfiguration
{
    public required string SigningCertPath { get; init; }
    
    public string? SigningCertPassword { get; init; }

    public required string EncryptionCertPath { get; init; }

    public string? EncryptionCertPassword { get; init; }
}
