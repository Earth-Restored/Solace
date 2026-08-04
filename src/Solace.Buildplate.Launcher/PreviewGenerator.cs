using System.Text;
using Microsoft.Extensions.Logging;
using Solace.Common;
using Solace.PreviewGenerator.Registry;

namespace Solace.Buildplate.Launcher;

internal static partial class PreviewGenerator
{
    public static string? GeneratePreview(byte[] serverData, bool isNight, string staticDataPath, ILogger logger)
    {
        BedrockBlocks.Initialize(staticDataPath, logger);
        JavaBlocks.Initialize(staticDataPath, logger);

        string previewString;
        try
        {
            using (var ms = new MemoryStream(serverData))
            {
                previewString = Solace.PreviewGenerator.Generator.Generate(ms, logger);
            }
        }
        catch (Exception exception)
        {
            LogErrorWhileGeneratingBuildplatePreview(logger, exception);
            return null;
        }

        // todo: use JsonObject?
        Dictionary<string, object> previewObject;
        try
        {
            previewObject = Json.Deserialize<Dictionary<string, object>>(previewString)!;
        }
        catch (Exception exception)
        {
            LogErrorWhileJsonEncoding(logger, exception);
            return null;
        }

        previewObject["isNight"] = isNight;

        var previewJson = Json.Serialize(previewObject);

        var previewBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(previewJson));

        LogPreviewGenerated(logger);
        return previewBase64;
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Error while generating buildplate preview")]
    private static partial void LogErrorWhileGeneratingBuildplatePreview(ILogger logger, Exception exception);

    [LoggerMessage(Level = LogLevel.Error, Message = "Error while processing buildplate preview generator response")]
    private static partial void LogErrorWhileJsonEncoding(ILogger logger, Exception exception);

    [LoggerMessage(Level = LogLevel.Information, Message = "Preview generated")]
    private static partial void LogPreviewGenerated(ILogger logger);
}
