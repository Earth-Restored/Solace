using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using Solace.Common;

namespace Solace.Buildplate.PreviewGenerator.Utils;

public static partial class DataFile
{
    public static void Load(string path, ILogger logger, Action<JsonNode> consumer)
    {
        try
        {
            consumer(Json.Deserialize<JsonNode>(File.ReadAllText(path))!);
        }
        catch (Exception exception)
        {
            LogFailedToReadResource(logger, exception, path);
            throw;
        }
    }

    [LoggerMessage(Level = LogLevel.Critical, Message = "Failed to read resource '{Path}'")]
    private static partial void LogFailedToReadResource(ILogger logger, Exception exception, string Path);
}
