namespace Solace.Common.Asp.Captcha;

public sealed class NoOpCaptchaValidator : ICaptchaValidator
{
    public string Script => string.Empty;

    public string FormFieldName => "captcha-noop";

    public string GetHtmlWidget(string size = "normal")
        => """
        <input type="hidden" id="captcha-noop" value="abc" name="captcha-noop" />
        """;

    public async Task<bool> ValidateAsync(string? token, string? remoteip = null, CancellationToken cancellationToken = default)
        => true;
}