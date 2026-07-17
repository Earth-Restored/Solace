namespace Solace.Common.Asp.Captcha;

public interface ICaptchaValidator
{
    string Script { get; }

    string FormFieldName { get; }

    string GetHtmlWidget(string size = "normal");

    Task<bool> ValidateAsync(string? token, string? remoteip = null, CancellationToken cancellationToken = default);
}