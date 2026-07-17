namespace Solace.AuthServer.Features.Live.Login.Infrastructure;

public sealed class NoOpCaptchaValidator : ICaptchaValidator
{
    public string Script => string.Empty;

    public string HtmlWidget => string.Empty;

    public async Task<bool> ValidateAsync(string token, string? remoteip = null, CancellationToken cancellationToken = default)
        => true;
}