namespace Solace.Common.Asp.Captcha;

public interface ICaptchaValidator
{
    string FormFieldName { get; }

    string Script { get; }

    string ManagerScript { get; }

    Task<bool> ValidateAsync(string? token, string? remoteIp = null, CancellationToken cancellationToken = default);
}
