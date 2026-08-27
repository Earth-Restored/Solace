using System.Text.Json;
using System.Text.Json.Serialization;

namespace Solace.LauncherUI;

public sealed class LogEvent
{
    public DateTime Timestamp { get; set; }

    public string? Level { get; set; }

    public string? RenderedMessage { get; set; }

    public string? Exception { get; set; }

    public LogEventProperties? Properties { get; set; }
}

public sealed class LogEventProperties
{
    public string? ComponentName { get; set; }

    // [JsonExtensionData]
    // public IDictionary<string, JsonElement>? AdditionalProperties { get; set; }
}