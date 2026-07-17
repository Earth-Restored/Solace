namespace Solace.AuthServer.Features.Live.Login;

public sealed class CaptchaOptions
{
    public CaptchaProvider Provider { get; init; } = CaptchaProvider.NoOp;

    public string? CloudflareTurnstileSiteKey { get; init; }
    
    public string? CloudflareTurnstileSecretKey { get; init; }
}

public enum CaptchaProvider
{
    NoOp,
    CloudflareTurnstile,
}