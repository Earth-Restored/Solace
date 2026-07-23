namespace Solace.Common.Asp.Captcha;

public sealed class NoOpCaptchaValidator : ICaptchaValidator
{
    public string FormFieldName => "captcha-noop";

    public string Script => string.Empty;

    public string ManagerScript { get; } = """
        class CaptchaManager {
            constructor() {
                this.widgets = new Map();
            }

            createWidget(containerId, config) {
                this.widgets.set(containerId, config);

                if (config && config.onSuccess) {
                    config.onSuccess("abc", "abc");
                }

                return "abc";
            }

            removeWidget(containerId) {
                this.widgets.delete(containerId);
            }

            resetWidget(containerId) {
                const config = this.widgets.get(containerId);

                if (config && config.onSuccess) {
                    config.onSuccess("abc", "abc");
                }
            }
        }
        """;

    public async Task<bool> ValidateAsync(string? token, string? remoteIp = null, CancellationToken cancellationToken = default)
        => true;
}