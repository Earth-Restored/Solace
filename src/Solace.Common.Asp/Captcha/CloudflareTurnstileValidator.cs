using System.Net;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Solace.Common.Asp.Captcha;

public sealed partial class CloudflareTurnstileValidator(
    HttpClient httpClient,
    IOptions<CaptchaConfiguration> captchaOptions,
    ILogger<CloudflareTurnstileValidator> logger) : ICaptchaValidator
{
    private const string VerifyUrl = "https://challenges.cloudflare.com/turnstile/v0/siteverify";

    public string FormFieldName => "cf-turnstile-response";

    public string Script => """
        <link rel="preconnect" href="https://challenges.cloudflare.com" />
        <script src="https://challenges.cloudflare.com/turnstile/v0/api.js?render=explicit&onload=onloadTurnstileCallback" defer></script>
        """;

    public string ManagerScript { get; } = $$"""
        class CaptchaManager {
            constructor() {
                this.widgets = new Map();
                this.pending = [];
                this._blazorHooked = false;

                this._ensureBlazorHook();
            }

            _ensureBlazorHook() {
                if (window.Blazor && !this._blazorHooked) {
                    this._blazorHooked = true;
                    Blazor.addEventListener('enhancedload', () => this.renderAll());
                }
            }

            renderAll() {
                this._ensureBlazorHook();

                for (const [containerId, widgetId] of this.widgets.entries()) {
                    const el = document.querySelector(containerId);
                    if (!el || el.childElementCount === 0) {
                        try { window.turnstile && window.turnstile.remove(widgetId); } catch (e) {}
                        this.widgets.delete(containerId);
                    }
                }

                if (!window.turnstile) {
                    return;
                }

                const autoContainers = document.querySelectorAll('[data-captcha="true"]');
                autoContainers.forEach(container => {
                    if (container.id) {
                        this.createWidget('#' + container.id);
                    }
                });

                const currentPending = [...this.pending];
                this.pending = [];

                for (const item of currentPending) {
                    this.createWidget(item.containerId, item.config);
                }
            }

            createWidget(containerId, config = {}) {
                const container = document.querySelector(containerId);
                if (!container) {
                    return;
                }

                if (!window.turnstile) {
                    this.pending.push({ containerId, config });
                    return;
                }

                if (this.widgets.has(containerId) && container.childElementCount > 0) {
                    return;
                }

                if (this.widgets.has(containerId)) {
                    try {
                        window.turnstile.remove(this.widgets.get(containerId));
                    } catch (e) {
                    }

                    this.widgets.delete(containerId);
                }

                const widgetId = window.turnstile.render(containerId, {
                    sitekey: "{{captchaOptions.Value.CloudflareTurnstileSiteKey}}",
                    theme: config.theme || "auto",
                    size: config.size || "normal",
                    callback: (token) => {
                        if (config.onSuccess) {
                            config.onSuccess(token, widgetId);
                        }
                    },
                    "error-callback": (error) => {
                        if (config.onError) {
                            config.onError(error, widgetId);
                        }
                    }
                });

                this.widgets.set(containerId, widgetId);
                return widgetId;
            }

            removeWidget(containerId) {
                const widgetId = this.widgets.get(containerId);
                if (widgetId && window.turnstile) {
                    try {
                        window.turnstile.remove(widgetId);
                    } catch (e) {
                    }

                    this.widgets.delete(containerId);
                }
            }

            resetWidget(containerId) {
                const widgetId = this.widgets.get(containerId);
                if (widgetId && window.turnstile) {
                    try {
                    window.turnstile.reset(widgetId);
                    } catch (e) {
                    }
                }
            }
        }

        window.captchaManager = window.captchaManager || new CaptchaManager();
        window.onloadTurnstileCallback = () => window.captchaManager.renderAll();
        """;

    public async Task<bool> ValidateAsync(string? token, string? remoteIp = null, CancellationToken cancellationToken = default)
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
            var parameters = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                { "secret", secretKey },
                { "response", token }
            };

            if (!string.IsNullOrEmpty(remoteIp))
            {
                parameters.Add("remoteip", remoteIp);
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
