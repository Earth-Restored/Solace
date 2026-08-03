namespace Solace.Common.Asp.Captcha;

public sealed class CaptchaConfiguration
{
    public CaptchaProvider Provider { get; init; } = CaptchaProvider.NoOp;

    public string? CloudflareTurnstileSiteKey { get; init; }

    public string? CloudflareTurnstileSecretKey { get; init; }
}
