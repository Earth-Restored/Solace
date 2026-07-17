using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Solace.Common.Asp.Captcha;

public sealed partial class CloudflareTurnstileValidator(
    HttpClient httpClient,
    IOptions<CaptchaOptions> captchaOptions,
    ILogger<CloudflareTurnstileValidator> logger) : ICaptchaValidator
{
    private const string VerifyUrl = "https://challenges.cloudflare.com/turnstile/v0/siteverify";

    public string Script { get; } = """
        <link rel="preconnect" href="https://challenges.cloudflare.com" />
        <script src="https://challenges.cloudflare.com/turnstile/v0/api.js" async defer></script>
        """;

    public string FormFieldName => "cf-turnstile-response";

    public string GetHtmlWidget(string size = "normal")
        => $"""
        <div class="cf-turnstile" data-sitekey="{captchaOptions.Value.CloudflareTurnstileSiteKey}" data-size="{size}"></div>
        """;

    public async Task<bool> ValidateAsync(string? token, string? remoteip = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        var secretKey = captchaOptions.Value.CloudflareTurnstileSecretKey;

        if (string.IsNullOrWhiteSpace(secretKey))
        {
            LogSecretKeyMissing();
            return false;
        }

        try
        {
            var parameters = new Dictionary<string, string>
            {
                { "secret", secretKey },
                { "response", token }
            };

            if (!string.IsNullOrEmpty(remoteip))
            {
                parameters.Add("remoteip", remoteip);
            }

            var postContent = new FormUrlEncodedContent(parameters);

            var response = await httpClient.PostAsync(VerifyUrl, postContent, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                LogApiError(response.StatusCode);
                return false;
            }

            var result = await response.Content.ReadFromJsonAsync<TurnstileResponse>(cancellationToken);

            if (result is null || !result.Success)
            {
                LogValidationError(result?.ErrorCodes is not null ? string.Join(", ", result.ErrorCodes) : "none");
                return false;
            }

            return true;
        }
        catch (Exception exception)
        {
            LogUnknownError(exception);
            return false;
        }
    }

    private sealed record TurnstileResponse
    {
        [JsonPropertyName("success")]
        public bool Success { get; init; }

        [JsonPropertyName("error-codes")]
        public string[]? ErrorCodes { get; init; }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Cloudflare Turnstile secret key is missing in configuration")]
    private partial void LogSecretKeyMissing();

    [LoggerMessage(Level = LogLevel.Error, Message = "Turnstile API returned non-success status code: {StatusCode}")]
    private partial void LogApiError(HttpStatusCode StatusCode);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Turnstile validation failed. Errors: {Errors}")]
    private partial void LogValidationError(string Errors);

    [LoggerMessage(Level = LogLevel.Debug, Message = "An error occurred while validating the Turnstile token")]
    private partial void LogUnknownError(Exception exception);
}
