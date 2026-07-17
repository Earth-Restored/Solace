namespace Solace.AuthServer.Features.Live.Login.Infrastructure;

public interface ICaptchaValidator
{
    string Script { get; }

    string HtmlWidget { get; }

    Task<bool> ValidateAsync(string token, string? remoteip = null, CancellationToken cancellationToken = default);
}